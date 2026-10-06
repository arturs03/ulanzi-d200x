#![forbid(unsafe_code)]

use d200x_provider_protocol::SensorLogConfig;
use std::{
    fs::File,
    io::{Read, Seek, SeekFrom},
    path::{Path, PathBuf},
};
use time::{
    format_description::well_known::Rfc3339, Date, Month, OffsetDateTime, PrimitiveDateTime, Time,
};

const LIMIT: usize = 65_536;
pub struct SensorLog {
    config: SensorLogConfig,
}
pub struct Observation {
    pub values: [Option<f64>; 4],
    pub observed: String,
}

fn sensor_id(id: &str, cpu: bool, kind: &str) -> bool {
    let parts: Vec<_> = id.split('/').collect();
    parts.len() == 5
        && parts[0].is_empty()
        && parts[3] == kind
        && if cpu {
            matches!(parts[1], "amdcpu" | "intelcpu")
        } else {
            matches!(parts[1], "nvidiagpu" | "atigpu")
        }
        && [parts[2], parts[4]]
            .iter()
            .all(|n| !n.is_empty() && n.len() <= 3 && n.bytes().all(|b| b.is_ascii_digit()))
}

impl SensorLog {
    pub fn new(config: SensorLogConfig) -> Result<Self, &'static str> {
        let path = Path::new(&config.directory);
        let bytes = config.directory.as_bytes();
        if bytes.len() < 3
            || bytes.len() > 1024
            || !bytes[0].is_ascii_alphabetic()
            || bytes[1] != b':'
            || bytes[2] != b'\\'
            || config.directory.chars().any(char::is_control)
            || config.directory[2..].contains(':')
            || path.components().any(|c| {
                matches!(
                    c,
                    std::path::Component::ParentDir | std::path::Component::CurDir
                )
            })
            || !super::log_clock::matches_zone(&config.time_zone_id)
            || !super::windows::local_log_drive(&config.directory)
        {
            return Err("invalid-settings");
        }
        if config
            .cpu_temperature
            .as_deref()
            .is_some_and(|id| !sensor_id(id, true, "temperature"))
            || [
                config.gpu_temperature.as_deref(),
                config.gpu_hotspot.as_deref(),
            ]
            .into_iter()
            .flatten()
            .any(|id| !sensor_id(id, false, "temperature"))
            || config
                .gpu_usage
                .as_deref()
                .is_some_and(|id| !sensor_id(id, false, "load"))
            || [
                config.cpu_temperature.as_ref(),
                config.gpu_temperature.as_ref(),
                config.gpu_hotspot.as_ref(),
                config.gpu_usage.as_ref(),
            ]
            .iter()
            .all(|id| id.is_none())
        {
            return Err("invalid-settings");
        }
        if let (Some(core), Some(hotspot)) = (&config.gpu_temperature, &config.gpu_hotspot) {
            if core == hotspot
                || core.rsplit_once("/temperature/").map(|p| p.0)
                    != hotspot.rsplit_once("/temperature/").map(|p| p.0)
            {
                return Err("ambiguous-gpu");
            }
        }
        let hardware: Vec<_> = [
            &config.gpu_temperature,
            &config.gpu_hotspot,
            &config.gpu_usage,
        ]
        .into_iter()
        .flatten()
        .map(|id| id.split('/').take(3).collect::<Vec<_>>())
        .collect();
        if hardware.windows(2).any(|pair| pair[0] != pair[1]) {
            return Err("ambiguous-gpu");
        }
        Ok(Self { config })
    }

    pub fn read(&self, now: OffsetDateTime) -> Result<Observation, &'static str> {
        if !super::windows::local_log_drive(&self.config.directory) {
            return Err("source-unavailable");
        }
        if !super::log_clock::matches_zone(&self.config.time_zone_id) {
            return Err("time-zone-changed");
        }
        let date = super::log_clock::local(now)
            .ok_or("invalid-time-zone")?
            .date();
        let path = daily_path(Path::new(&self.config.directory), date);
        check_path(&path)?;
        let (headers, row) = read_bounded(&path)?;
        parse_observation(&headers, &row, &self.config, now, super::log_clock::utc)
    }
}

fn daily_path(folder: &Path, date: Date) -> PathBuf {
    folder.join(format!(
        "OpenHardwareMonitorLog-{:04}-{:02}-{:02}.csv",
        date.year(),
        u8::from(date.month()),
        date.day()
    ))
}

fn check_path(path: &Path) -> Result<(), &'static str> {
    for current in path.ancestors() {
        let metadata = std::fs::symlink_metadata(current).map_err(|_| "source-unavailable")?;
        #[cfg(windows)]
        {
            use std::os::windows::fs::MetadataExt;
            if metadata.file_attributes() & 0x400 != 0 {
                return Err("linked-source");
            }
        }
        if metadata.file_type().is_symlink() {
            return Err("linked-source");
        }
    }
    Ok(())
}

fn read_bounded(path: &Path) -> Result<(String, String), &'static str> {
    let mut options = File::options();
    options.read(true);
    #[cfg(windows)]
    {
        use std::os::windows::fs::OpenOptionsExt;
        options.share_mode(7);
    }
    let mut file = options.open(path).map_err(|_| "source-unavailable")?;
    let size = file.metadata().map_err(|_| "source-unavailable")?.len();
    if !file.metadata().map_err(|_| "source-unavailable")?.is_file() {
        return Err("source-unavailable");
    }
    let mut prefix = Vec::new();
    Read::by_ref(&mut file)
        .take(LIMIT as u64)
        .read_to_end(&mut prefix)
        .map_err(|_| "source-unavailable")?;
    let ends: Vec<_> = prefix
        .iter()
        .enumerate()
        .filter_map(|(i, b)| (*b == b'\n').then_some(i))
        .take(2)
        .collect();
    if ends.len() != 2 {
        return Err("invalid-header");
    }
    let header_end = ends[1] + 1;
    let headers = std::str::from_utf8(&prefix[..ends[1]])
        .map_err(|_| "invalid-header")?
        .to_owned();
    let start = size.saturating_sub(LIMIT as u64).max(header_end as u64);
    file.seek(SeekFrom::Start(start))
        .map_err(|_| "source-unavailable")?;
    let mut tail = Vec::new();
    Read::by_ref(&mut file)
        .take(size.saturating_sub(start))
        .read_to_end(&mut tail)
        .map_err(|_| "source-unavailable")?;
    if tail.len() as u64 != size.saturating_sub(start) {
        return Err("source-changed");
    }
    if file.metadata().map_err(|_| "source-unavailable")?.len() < size {
        return Err("source-changed");
    }
    file.seek(SeekFrom::Start(0))
        .map_err(|_| "source-unavailable")?;
    let mut checked_header = vec![0; header_end];
    file.read_exact(&mut checked_header)
        .map_err(|_| "source-changed")?;
    if checked_header != prefix[..header_end] {
        return Err("source-changed");
    }
    let row = last_complete_row(&tail, start > header_end as u64)?;
    Ok((
        headers,
        std::str::from_utf8(row)
            .map_err(|_| "invalid-row")?
            .trim_end_matches('\r')
            .to_owned(),
    ))
}

fn last_complete_row(tail: &[u8], starts_mid_row: bool) -> Result<&[u8], &'static str> {
    let begin = if starts_mid_row {
        tail.iter().position(|b| *b == b'\n').ok_or("partial-row")? + 1
    } else {
        0
    };
    let end = tail
        .iter()
        .rposition(|b| *b == b'\n')
        .ok_or("partial-row")?;
    if end < begin {
        return Err("partial-row");
    }
    let start = tail[begin..end]
        .iter()
        .rposition(|b| *b == b'\n')
        .map_or(begin, |n| begin + n + 1);
    Ok(&tail[start..end])
}

fn csv(line: &str) -> Result<Vec<String>, &'static str> {
    let mut fields = Vec::new();
    let mut field = String::new();
    let mut chars = line.chars().peekable();
    let (mut quoted, mut closed) = (false, false);
    while let Some(c) = chars.next() {
        if quoted {
            if c == '"' {
                if chars.peek() == Some(&'"') {
                    chars.next();
                    field.push('"');
                } else {
                    quoted = false;
                    closed = true;
                }
            } else {
                field.push(c);
            }
        } else if c == ',' {
            fields.push(std::mem::take(&mut field));
            closed = false;
        } else if c == '"' && field.is_empty() && !closed {
            quoted = true;
        } else if c == '"' || closed {
            return Err("invalid-csv");
        } else {
            field.push(c);
        }
    }
    if quoted {
        return Err("invalid-csv");
    }
    fields.push(field);
    if fields.len() > 1025 {
        return Err("invalid-csv");
    }
    Ok(fields)
}

fn parse_local(text: &str) -> Option<PrimitiveDateTime> {
    let b = text.as_bytes();
    if b.len() != 19
        || b[2] != b'/'
        || b[5] != b'/'
        || b[10] != b' '
        || b[13] != b':'
        || b[16] != b':'
        || b.iter()
            .enumerate()
            .any(|(i, b)| ![2, 5, 10, 13, 16].contains(&i) && !b.is_ascii_digit())
    {
        return None;
    }
    let date = Date::from_calendar_date(
        text[6..10].parse().ok()?,
        Month::try_from(text[0..2].parse::<u8>().ok()?).ok()?,
        text[3..5].parse().ok()?,
    )
    .ok()?;
    let clock = Time::from_hms(
        text[11..13].parse().ok()?,
        text[14..16].parse().ok()?,
        text[17..19].parse().ok()?,
    )
    .ok()?;
    Some(PrimitiveDateTime::new(date, clock))
}

fn parse_observation(
    headers: &str,
    row: &str,
    config: &SensorLogConfig,
    now: OffsetDateTime,
    to_utc: impl FnOnce(PrimitiveDateTime) -> Option<OffsetDateTime>,
) -> Result<Observation, &'static str> {
    let header: Vec<_> = headers.trim_start_matches('\u{feff}').split('\n').collect();
    if header.len() != 2 {
        return Err("invalid-header");
    }
    let ids = csv(header[0].trim_end_matches('\r'))?;
    let names = csv(header[1].trim_end_matches('\r'))?;
    if ids.len() < 2
        || ids.len() != names.len()
        || !ids[0].is_empty()
        || names[0] != "Time"
        || ids.iter().skip(1).any(|id| id.is_empty() || id.len() > 256)
        || names
            .iter()
            .any(|name| name.len() > 256 || name.chars().any(char::is_control))
        || ids
            .iter()
            .skip(1)
            .collect::<std::collections::HashSet<_>>()
            .len()
            != ids.len() - 1
    {
        return Err("ambiguous-header");
    }
    let values = csv(row)?;
    if values.len() != ids.len() {
        return Err("invalid-row");
    }
    let observed = parse_local(&values[0])
        .and_then(to_utc)
        .ok_or("ambiguous-time")?;
    let age = now - observed;
    if age > time::Duration::seconds(5) || age < time::Duration::seconds(-2) {
        return Err("stale-source");
    }
    let selected = [
        &config.cpu_temperature,
        &config.gpu_temperature,
        &config.gpu_hotspot,
        &config.gpu_usage,
    ];
    let expected = ["CPU Package", "GPU Core", "GPU Hot Spot", "GPU Core"];
    let temperatures = std::array::from_fn(|i| {
        let id = selected[i].as_ref()?;
        let index = ids.iter().position(|column| column == id)?;
        // Name and identifier both match. A reordered/renamed sensor is never silently substituted.
        if !names[index].eq_ignore_ascii_case(expected[i]) {
            return None;
        }
        let value = values[index].parse::<f64>().ok()?;
        let range = if i == 3 { 0.0..=100.0 } else { -50.0..=200.0 };
        (value.is_finite() && range.contains(&value)).then_some(value)
    });
    Ok(Observation {
        values: temperatures,
        observed: observed.format(&Rfc3339).map_err(|_| "invalid-time")?,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    const HEADER: &str = ",/amdcpu/0/temperature/0,/nvidiagpu/0/temperature/0,/nvidiagpu/0/temperature/1\nTime,\"CPU Package\",\"GPU Core\",\"GPU Hot Spot\"";
    fn config() -> SensorLogConfig {
        SensorLogConfig {
            directory: "C:\\fixture".into(),
            time_zone_id: "fixture".into(),
            cpu_temperature: Some("/amdcpu/0/temperature/0".into()),
            gpu_temperature: Some("/nvidiagpu/0/temperature/0".into()),
            gpu_hotspot: Some("/nvidiagpu/0/temperature/1".into()),
            gpu_usage: None,
        }
    }
    fn stamp() -> OffsetDateTime {
        parse_local("10/05/2026 12:00:00").unwrap().assume_utc()
    }
    fn parse(header: &str, row: &str, now: OffsetDateTime) -> Result<Observation, &'static str> {
        parse_observation(header, row, &config(), now, |t| Some(t.assume_utc()))
    }
    #[test]
    fn temperatures_are_independent_and_keep_source_time() {
        let sample = parse(HEADER, "10/05/2026 12:00:00,55,62,78", stamp()).unwrap();
        assert_eq!(sample.values, [Some(55.0), Some(62.0), Some(78.0), None]);
        assert_eq!(sample.observed, "2026-10-05T12:00:00Z");
        assert_eq!(
            parse(HEADER, "10/05/2026 12:00:00,,62,NaN", stamp())
                .unwrap()
                .values,
            [None, Some(62.0), None, None]
        );
        assert_eq!(
            parse(HEADER, "10/05/2026 12:00:00,201,62,Infinity", stamp())
                .unwrap()
                .values,
            [None, Some(62.0), None, None]
        );
        assert_eq!(
            parse(
                &HEADER.replace("GPU Hot Spot", "GPU Core"),
                "10/05/2026 12:00:00,55,62,78",
                stamp()
            )
            .unwrap()
            .values[2],
            None
        );
    }
    #[test]
    fn gpu_load_is_exact_bounded_and_independent() {
        let header = ",/amdcpu/0/temperature/0,/nvidiagpu/0/temperature/0,/nvidiagpu/0/temperature/1,/nvidiagpu/0/load/0,/nvidiagpu/0/load/1\nTime,\"CPU Package\",\"GPU Core\",\"GPU Hot Spot\",\"GPU Core\",\"GPU Memory\"";
        let mut settings = config();
        settings.gpu_usage = Some("/nvidiagpu/0/load/0".into());
        assert!(sensor_id("/nvidiagpu/0/load/0", false, "load"));
        assert!(!sensor_id("/nvidiagpu/0/temperature/0", false, "load"));
        for (load, expected) in [
            ("0", Some(0.0)),
            ("74", Some(74.0)),
            ("100", Some(100.0)),
            ("101", None),
            ("-1", None),
            ("NaN", None),
            ("", None),
        ] {
            let reading = parse_observation(
                header,
                &format!("10/05/2026 12:00:00,,62,,{load},99"),
                &settings,
                stamp(),
                |t| Some(t.assume_utc()),
            )
            .unwrap();
            assert_eq!(reading.values, [None, Some(62.0), None, expected]);
            assert_eq!(reading.observed, "2026-10-05T12:00:00Z");
        }
        settings.gpu_usage = Some("/nvidiagpu/0/load/1".into());
        assert_eq!(
            parse_observation(
                header,
                "10/05/2026 12:00:00,55,62,78,74,99",
                &settings,
                stamp(),
                |t| Some(t.assume_utc())
            )
            .unwrap()
            .values[3],
            None
        );
    }
    #[test]
    fn rejects_stale_future_ambiguous_and_malformed_rows() {
        for delta in [6, -3] {
            assert!(parse(
                HEADER,
                "10/05/2026 12:00:00,55,62,78",
                stamp() + time::Duration::seconds(delta)
            )
            .is_err());
        }
        for row in [
            "bad,55,62,78",
            "10/05/2026 12:00:00,55,62",
            "10/05/2026 12:00:00,55,62,78,90",
            "10/05/2026 12:00:00,\"55,62,78",
        ] {
            assert!(parse(HEADER, row, stamp()).is_err());
        }
        assert!(parse(
            &HEADER.replace("/temperature/1", "/temperature/0"),
            "10/05/2026 12:00:00,55,62,78",
            stamp()
        )
        .is_err());
        assert!(parse_observation(
            HEADER,
            "10/05/2026 12:00:00,55,62,78",
            &config(),
            stamp(),
            |_| None
        )
        .is_err());
        assert_eq!(
            csv("Time,\"A, B\",\"C\"\"D\"").unwrap(),
            ["Time", "A, B", "C\"D"]
        );
    }
    #[test]
    fn incomplete_append_never_becomes_an_observation() {
        assert_eq!(
            last_complete_row(b"old\ncomplete\r\npartial", false).unwrap(),
            b"complete\r"
        );
        assert_eq!(
            last_complete_row(b"partial\ncomplete\nnext", true).unwrap(),
            b"complete"
        );
        assert!(last_complete_row(b"partial\nnext", true).is_err());
        assert!(last_complete_row(b"partial", false).is_err());
    }
    #[test]
    fn bounded_reader_and_daily_rollover_use_only_complete_rows() {
        let folder = std::env::temp_dir().join(format!("d200x-log-test-{}", std::process::id()));
        std::fs::create_dir_all(&folder).unwrap();
        let path = daily_path(&folder, stamp().date());
        let mut content = format!("{HEADER}\n");
        content.push_str(&"10/05/2026 11:00:00,10,20,30\n".repeat(10000));
        content.push_str("10/05/2026 12:00:00,55,62,78\npartial");
        std::fs::write(&path, content).unwrap();
        let (header, row) = read_bounded(&path).unwrap();
        assert_eq!(parse(&header, &row, stamp()).unwrap().values[2], Some(78.0));
        assert_ne!(
            path,
            daily_path(&folder, stamp().date().next_day().unwrap())
        );
        std::fs::write(&path, "x".repeat(LIMIT + 1)).unwrap();
        assert!(read_bounded(&path).is_err());
        std::fs::remove_file(path).unwrap();
        std::fs::remove_dir(folder).unwrap();
    }
}
