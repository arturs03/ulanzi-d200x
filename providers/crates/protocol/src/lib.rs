#![forbid(unsafe_code)]

use serde::{Deserialize, Serialize};
use std::io::{self, BufRead, Write};
use time::{format_description::well_known::Rfc3339, OffsetDateTime};

pub const VERSION: u32 = 1;
pub const MAXIMUM_BYTES: usize = 65_536;
pub const MAXIMUM_SELECTIONS: usize = 64;

#[derive(Clone, Debug, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Selection {
    pub metric_id: String,
    pub source_id: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Request {
    pub protocol_version: u32,
    pub request_id: u32,
    #[serde(rename = "type")]
    pub kind: RequestKind,
    pub selections: Option<Vec<Selection>>,
    pub sensor_log: Option<SensorLogConfig>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct SensorLogConfig {
    pub directory: String,
    pub time_zone_id: String,
    pub cpu_temperature: Option<String>,
    pub gpu_temperature: Option<String>,
    pub gpu_hotspot: Option<String>,
    pub gpu_usage: Option<String>,
}

#[derive(Clone, Copy, Debug, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "lowercase")]
pub enum RequestKind {
    Hello,
    Snapshot,
    Shutdown,
}

#[derive(Clone, Copy, Debug, Serialize)]
#[serde(rename_all = "lowercase")]
pub enum Status {
    Ok,
    Unavailable,
    Error,
}

#[derive(Clone, Copy, Debug, Serialize)]
#[serde(rename_all = "lowercase")]
pub enum Unit {
    Percent,
    Celsius,
    Usd,
    Fps,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Sample {
    pub metric_id: String,
    pub source_id: String,
    pub value: Option<f64>,
    pub unit: Unit,
    pub observed_at: Option<String>,
    pub status: Status,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub code: Option<String>,
}

impl Sample {
    pub fn unavailable(selection: &Selection, unit: Unit, code: &str) -> Self {
        Self {
            metric_id: selection.metric_id.clone(),
            source_id: selection.source_id.clone(),
            value: None,
            unit,
            observed_at: None,
            status: Status::Unavailable,
            code: Some(code.to_owned()),
        }
    }

    pub fn reading(selection: &Selection, unit: Unit, value: f64, observed_at: &str) -> Self {
        if !value.is_finite() {
            return Self::unavailable(selection, unit, "invalid-value");
        }
        Self {
            metric_id: selection.metric_id.clone(),
            source_id: selection.source_id.clone(),
            value: Some(value),
            unit,
            observed_at: Some(observed_at.to_owned()),
            status: Status::Ok,
            code: None,
        }
    }
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Response {
    pub protocol_version: u32,
    pub request_id: u32,
    #[serde(rename = "type")]
    pub kind: RequestKind,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub provider_id: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub samples: Option<Vec<Sample>>,
}

pub fn now() -> io::Result<String> {
    OffsetDateTime::now_utc()
        .format(&Rfc3339)
        .map_err(io::Error::other)
}

pub fn valid_id(value: &str) -> bool {
    if value.is_empty() || value.len() > 96 || !value.as_bytes()[0].is_ascii_lowercase() {
        return false;
    }
    let mut separator = false;
    for byte in value.bytes() {
        if byte == b'.' || byte == b'-' {
            if separator {
                return false;
            }
            separator = true;
        } else if byte.is_ascii_lowercase() || byte.is_ascii_digit() {
            separator = false;
        } else {
            return false;
        }
    }
    !separator
}

// fill_buf limits allocation before parsing, including a line with no newline.
pub fn read_frame(reader: &mut impl BufRead) -> io::Result<Option<Vec<u8>>> {
    let mut frame = Vec::with_capacity(4096);
    loop {
        let buffer = reader.fill_buf()?;
        if buffer.is_empty() {
            return if frame.is_empty() {
                Ok(None)
            } else {
                Err(io::Error::new(
                    io::ErrorKind::UnexpectedEof,
                    "partial frame",
                ))
            };
        }
        let newline = buffer.iter().position(|byte| *byte == b'\n');
        let count = newline.unwrap_or(buffer.len());
        if frame.len() + count > MAXIMUM_BYTES {
            return Err(io::Error::new(
                io::ErrorKind::InvalidData,
                "oversized frame",
            ));
        }
        frame.extend_from_slice(&buffer[..count]);
        reader.consume(count + usize::from(newline.is_some()));
        if newline.is_some() {
            if frame.last() == Some(&b'\r') {
                frame.pop();
            }
            if frame.is_empty() {
                return Err(io::Error::new(io::ErrorKind::InvalidData, "empty frame"));
            }
            return Ok(Some(frame));
        }
    }
}

pub fn parse_request(frame: &[u8], previous_id: u32) -> io::Result<Request> {
    if frame.is_empty() || frame.len() > MAXIMUM_BYTES {
        return Err(io::Error::new(io::ErrorKind::InvalidData, "invalid frame"));
    }
    let request: Request = serde_json::from_slice(frame).map_err(io::Error::other)?;
    if request.protocol_version != VERSION
        || request.request_id <= previous_id
        || request.request_id > i32::MAX as u32
    {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            "invalid version or ID",
        ));
    }
    match (&request.kind, &request.selections) {
        (RequestKind::Snapshot, Some(selections))
            if !selections.is_empty() && selections.len() <= MAXIMUM_SELECTIONS =>
        {
            for (index, selected) in selections.iter().enumerate() {
                if !valid_id(&selected.metric_id)
                    || !valid_id(&selected.source_id)
                    || selections[..index].contains(selected)
                {
                    return Err(io::Error::new(
                        io::ErrorKind::InvalidData,
                        "invalid selection",
                    ));
                }
            }
        }
        (RequestKind::Hello | RequestKind::Shutdown, None) => {}
        _ => {
            return Err(io::Error::new(
                io::ErrorKind::InvalidData,
                "invalid request shape",
            ))
        }
    }
    if request.sensor_log.is_some() && request.kind != RequestKind::Hello {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            "settings outside hello",
        ));
    }
    Ok(request)
}

pub fn write_response(writer: &mut impl Write, response: &Response) -> io::Result<()> {
    let bytes = serde_json::to_vec(response).map_err(io::Error::other)?;
    if bytes.len() > MAXIMUM_BYTES {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            "oversized response",
        ));
    }
    writer.write_all(&bytes)?;
    writer.write_all(b"\n")?;
    writer.flush()
}

pub fn serve(
    provider_id: &str,
    mut snapshot: impl FnMut(&[Selection]) -> io::Result<Vec<Sample>>,
) -> io::Result<()> {
    serve_with_setup(
        provider_id,
        (),
        |_, settings| {
            if settings.is_some() {
                return Err(io::Error::new(
                    io::ErrorKind::InvalidData,
                    "unsupported settings",
                ));
            }
            Ok(())
        },
        |_, selected| snapshot(selected),
    )
}

pub fn serve_with_setup<T>(
    provider_id: &str,
    mut state: T,
    mut setup: impl FnMut(&mut T, Option<SensorLogConfig>) -> io::Result<()>,
    mut snapshot: impl FnMut(&mut T, &[Selection]) -> io::Result<Vec<Sample>>,
) -> io::Result<()> {
    let mut reader = io::stdin().lock();
    let mut writer = io::stdout().lock();
    let mut previous_id = 0;
    let mut greeted = false;
    while let Some(frame) = read_frame(&mut reader)? {
        let request = parse_request(&frame, previous_id)?;
        previous_id = request.request_id;
        if (!greeted && request.kind != RequestKind::Hello)
            || (greeted && request.kind == RequestKind::Hello)
        {
            return Err(io::Error::new(
                io::ErrorKind::InvalidData,
                "invalid lifecycle",
            ));
        }
        let mut response = Response {
            protocol_version: VERSION,
            request_id: request.request_id,
            kind: request.kind,
            provider_id: None,
            samples: None,
        };
        match request.kind {
            RequestKind::Hello => {
                setup(&mut state, request.sensor_log)?;
                greeted = true;
                response.provider_id = Some(provider_id.to_owned());
            }
            RequestKind::Snapshot => {
                let selected = request.selections.as_deref().ok_or_else(|| {
                    io::Error::new(io::ErrorKind::InvalidData, "missing selections")
                })?;
                response.samples = Some(snapshot(&mut state, selected)?);
            }
            RequestKind::Shutdown => {
                write_response(&mut writer, &response)?;
                return Ok(());
            }
        }
        write_response(&mut writer, &response)?;
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Cursor;

    #[test]
    fn bounded_framing_handles_split_crlf_and_eof() {
        let mut reader = io::BufReader::with_capacity(2, Cursor::new(b"{}\r\n[]\n"));
        assert_eq!(read_frame(&mut reader).unwrap(), Some(b"{}".to_vec()));
        assert_eq!(read_frame(&mut reader).unwrap(), Some(b"[]".to_vec()));
        assert!(read_frame(&mut reader).unwrap().is_none());
        assert!(read_frame(&mut Cursor::new(b"{}")).is_err());
        assert!(read_frame(&mut Cursor::new(vec![b'x'; MAXIMUM_BYTES + 1])).is_err());
        assert!(read_frame(&mut Cursor::new(b"\n")).is_err());
    }

    #[test]
    fn requests_reject_unknown_duplicate_invalid_and_wrong_version_fields() {
        let valid = br#"{"protocolVersion":1,"requestId":1,"type":"hello"}"#;
        assert!(parse_request(valid, 0).is_ok());
        assert!(parse_request(valid, 1).is_err());
        for invalid in [
            br#"{"protocolVersion":2,"requestId":1,"type":"hello"}"#.as_slice(),
            br#"{"protocolVersion":1,"requestId":1,"requestId":2,"type":"hello"}"#,
            br#"{"protocolVersion":1,"requestId":1,"type":"hello","command":"run"}"#,
            br#"{"protocolVersion":1,"requestId":1,"type":"snapshot","selections":[]}"#,
        ] {
            assert!(parse_request(invalid, 0).is_err());
        }
        assert!(parse_request(&[255, 10], 0).is_err());
    }

    #[test]
    fn sensor_settings_are_typed_and_only_accepted_at_hello() {
        let hello = br#"{"protocolVersion":1,"requestId":1,"type":"hello","sensorLog":{"directory":"C:\\fixture","timeZoneId":"fixture","gpuHotspot":"/nvidiagpu/0/temperature/1"}}"#;
        let legacy = parse_request(hello, 0).unwrap();
        assert!(legacy.sensor_log.unwrap().gpu_usage.is_none());
        let load = std::str::from_utf8(hello).unwrap().replace(
            "\"gpuHotspot\":",
            "\"gpuUsage\":\"/nvidiagpu/0/load/0\",\"gpuHotspot\":",
        );
        assert_eq!(
            parse_request(load.as_bytes(), 0)
                .unwrap()
                .sensor_log
                .unwrap()
                .gpu_usage
                .as_deref(),
            Some("/nvidiagpu/0/load/0")
        );
        assert!(parse_request(hello, 0).unwrap().sensor_log.is_some());
        let text = String::from_utf8(hello.to_vec()).unwrap();
        assert!(parse_request(text.replace("\"hello\"", "\"shutdown\"").as_bytes(), 0).is_err());
        assert!(parse_request(
            text.replace(
                "\"timeZoneId\":\"fixture\"",
                "\"timeZoneId\":\"fixture\",\"timeZoneId\":\"again\""
            )
            .as_bytes(),
            0
        )
        .is_err());
        assert!(parse_request(text.replace("gpuHotspot", "command").as_bytes(), 0).is_err());
        assert!(parse_request(
            text.replace(
                "\"gpuHotspot\":\"/nvidiagpu/0/temperature/1\"",
                "\"gpuHotspot\":42"
            )
            .as_bytes(),
            0
        )
        .is_err());
    }
}
