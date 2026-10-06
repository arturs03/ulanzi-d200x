use d200x_provider_protocol::{self as protocol, Sample, Selection, Unit};
use std::{
    io,
    time::{Duration, Instant},
};
mod log_clock;
mod sensor_log;

#[derive(Clone, Copy)]
struct CpuTimes {
    idle: u64,
    kernel: u64,
    user: u64,
}

fn cpu_percent(previous: CpuTimes, current: CpuTimes) -> Option<f64> {
    let idle = current.idle.checked_sub(previous.idle)?;
    let kernel = current.kernel.checked_sub(previous.kernel)?;
    let user = current.user.checked_sub(previous.user)?;
    let total = kernel.checked_add(user)?;
    // Windows includes idle time in kernel time.
    if total == 0 || idle > total {
        return None;
    }
    Some(100.0 * (total - idle) as f64 / total as f64)
}

fn memory_percent(total: u64, available: u64) -> Option<f64> {
    if total == 0 || available > total {
        return None;
    }
    Some(100.0 * (total - available) as f64 / total as f64)
}

struct Collector {
    previous: Option<CpuTimes>,
    last: Option<Instant>,
    sensor_log: Option<sensor_log::SensorLog>,
}

impl Collector {
    fn snapshot(&mut self, selected: &[Selection]) -> io::Result<Vec<Sample>> {
        if selected.iter().any(|item| {
            !matches!(
                (item.source_id.as_str(), item.metric_id.as_str()),
                ("windows.system", "cpu.usage" | "ram.usage")
                    | (
                        "monitor.local",
                        "cpu.temperature" | "gpu.temperature" | "gpu.hotspot" | "gpu.usage"
                    )
            )
        }) {
            return Err(io::Error::new(
                io::ErrorKind::InvalidData,
                "unknown selection",
            ));
        }
        // Host cadence and this guard prevent repeated callers causing additional acquisition.
        if self
            .last
            .is_some_and(|last| last.elapsed() < Duration::from_millis(1000))
        {
            return Ok(selected
                .iter()
                .map(|item| Sample::unavailable(item, unit(item), "minimum-interval"))
                .collect());
        }
        self.last = Some(Instant::now());
        let wants_cpu = selected.iter().any(|item| item.metric_id == "cpu.usage");
        let cpu = if wants_cpu {
            let current = windows::cpu_times();
            let percent = self
                .previous
                .zip(current)
                .and_then(|(previous, current)| cpu_percent(previous, current));
            self.previous = current;
            percent
        } else {
            self.previous = None;
            None
        };
        let memory = if selected.iter().any(|item| item.metric_id == "ram.usage") {
            windows::memory().and_then(|(total, available)| memory_percent(total, available))
        } else {
            None
        };
        let observed = protocol::now()?;
        // One bounded shared log read for all selected temperatures; usage stays independent of log failure.
        let temperatures = if selected.iter().any(|s| s.source_id == "monitor.local") {
            self.sensor_log
                .as_ref()
                .ok_or("source-not-configured")
                .and_then(|log| log.read(time::OffsetDateTime::now_utc()))
        } else {
            Err("source-not-selected")
        };
        Ok(selected
            .iter()
            .map(|item| {
                if item.source_id == "monitor.local" {
                    let index = match item.metric_id.as_str() {
                        "cpu.temperature" => 0,
                        "gpu.temperature" => 1,
                        "gpu.hotspot" => 2,
                        _ => 3,
                    };
                    return match &temperatures {
                        Ok(reading) => match reading.values[index] {
                            Some(value) => {
                                Sample::reading(item, unit(item), value, &reading.observed)
                            }
                            None => Sample::unavailable(item, unit(item), "sensor-unavailable"),
                        },
                        Err(code) => Sample::unavailable(item, unit(item), code),
                    };
                }
                let value = if item.metric_id == "cpu.usage" {
                    cpu
                } else {
                    memory
                };
                match value {
                    Some(value) => Sample::reading(item, Unit::Percent, value, &observed),
                    None => Sample::unavailable(item, Unit::Percent, "source-unavailable"),
                }
            })
            .collect())
    }
}

fn unit(item: &Selection) -> Unit {
    if item.source_id == "monitor.local" && item.metric_id != "gpu.usage" {
        Unit::Celsius
    } else {
        Unit::Percent
    }
}

#[cfg(windows)]
mod windows {
    use super::CpuTimes;

    #[repr(C)]
    #[derive(Default)]
    struct FileTime {
        low: u32,
        high: u32,
    }
    impl FileTime {
        fn ticks(&self) -> u64 {
            (u64::from(self.high) << 32) | u64::from(self.low)
        }
    }
    #[repr(C)]
    #[derive(Default)]
    struct MemoryStatus {
        length: u32,
        load: u32,
        total_physical: u64,
        available_physical: u64,
        total_page: u64,
        available_page: u64,
        total_virtual: u64,
        available_virtual: u64,
        available_extended: u64,
    }
    #[link(name = "kernel32")]
    unsafe extern "system" {
        fn GetSystemTimes(idle: *mut FileTime, kernel: *mut FileTime, user: *mut FileTime) -> i32;
        fn GlobalMemoryStatusEx(status: *mut MemoryStatus) -> i32;
        fn GetActiveProcessorCount(group: u16) -> u32;
        fn GetDriveTypeW(root: *const u16) -> u32;
    }
    pub fn local_log_drive(path: &str) -> bool {
        let Some(root) = path.get(..3) else {
            return false;
        };
        let root: Vec<u16> = root.encode_utf16().chain(Some(0)).collect();
        // SAFETY: the UTF-16 root is null-terminated and retained throughout this read-only drive-type query.
        matches!(unsafe { GetDriveTypeW(root.as_ptr()) }, 2 | 3 | 6)
    }
    pub fn cpu_times() -> Option<CpuTimes> {
        let (mut idle, mut kernel, mut user) = (
            FileTime::default(),
            FileTime::default(),
            FileTime::default(),
        );
        // SAFETY: group 0xffff requests the total count and requires no pointer/handle.
        let count = unsafe { GetActiveProcessorCount(0xffff) };
        if count == 0 || count > 64 {
            return None;
        }
        // SAFETY: all pointers reference initialized, correctly aligned FILETIME layouts for this call.
        if unsafe { GetSystemTimes(&mut idle, &mut kernel, &mut user) } == 0 {
            return None;
        }
        Some(CpuTimes {
            idle: idle.ticks(),
            kernel: kernel.ticks(),
            user: user.ticks(),
        })
    }
    pub fn memory() -> Option<(u64, u64)> {
        let mut status = MemoryStatus {
            length: std::mem::size_of::<MemoryStatus>() as u32,
            ..Default::default()
        };
        // SAFETY: repr(C) matches MEMORYSTATUSEX, including its size field, and lives throughout the call.
        if unsafe { GlobalMemoryStatusEx(&mut status) } == 0 {
            return None;
        }
        Some((status.total_physical, status.available_physical))
    }
}

#[cfg(not(windows))]
mod windows {
    use super::CpuTimes;
    pub fn cpu_times() -> Option<CpuTimes> {
        None
    }
    pub fn memory() -> Option<(u64, u64)> {
        None
    }
    pub fn local_log_drive(_: &str) -> bool {
        false
    }
}

fn main() {
    let mut collector = Collector {
        previous: None,
        last: None,
        sensor_log: None,
    };
    if protocol::serve_with_setup(
        "d200x.system",
        &mut collector,
        |collector, settings| {
            collector.sensor_log = settings
                .map(sensor_log::SensorLog::new)
                .transpose()
                .map_err(io::Error::other)?;
            Ok(())
        },
        |collector, selected| collector.snapshot(selected),
    )
    .is_err()
    {
        eprintln!("system-provider-failed");
        std::process::exit(1);
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn cpu_counts_idle_once_and_rejects_reset_or_empty_interval() {
        let previous = CpuTimes {
            idle: 100,
            kernel: 200,
            user: 100,
        };
        let current = CpuTimes {
            idle: 125,
            kernel: 250,
            user: 150,
        };
        assert_eq!(cpu_percent(previous, current), Some(75.0));
        assert!(cpu_percent(current, previous).is_none());
        assert!(cpu_percent(previous, previous).is_none());
        assert!(cpu_percent(
            previous,
            CpuTimes {
                idle: 500,
                kernel: 250,
                user: 150
            }
        )
        .is_none());
    }
    #[test]
    fn memory_uses_physical_bytes_and_rejects_invalid_counters() {
        assert_eq!(memory_percent(1000, 400), Some(60.0));
        assert_eq!(memory_percent(1000, 1000), Some(0.0));
        assert!(memory_percent(0, 0).is_none());
        assert!(memory_percent(100, 101).is_none());
    }
}
