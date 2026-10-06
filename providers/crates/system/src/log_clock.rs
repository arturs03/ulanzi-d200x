use time::{OffsetDateTime, PrimitiveDateTime};

// Testable DST resolution: both Windows offsets are round-tripped; repeated/missing local times fail.
fn resolve(
    local: PrimitiveDateTime,
    biases: [i32; 2],
    roundtrip: impl Fn(OffsetDateTime) -> Option<PrimitiveDateTime>,
) -> Option<OffsetDateTime> {
    let mut result = None;
    for bias in biases {
        let candidate = local
            .assume_utc()
            .checked_add(time::Duration::minutes(i64::from(bias)))?;
        if roundtrip(candidate) == Some(local) {
            if result.is_some_and(|old| old != candidate) {
                return None;
            }
            result = Some(candidate);
        }
    }
    result
}

#[cfg(windows)]
mod native {
    use super::*;
    use time::{Date, Month, Time};
    #[repr(C)]
    #[derive(Clone, Copy, Default)]
    struct SystemTime {
        year: u16,
        month: u16,
        weekday: u16,
        day: u16,
        hour: u16,
        minute: u16,
        second: u16,
        milliseconds: u16,
    }
    impl SystemTime {
        fn from_time(value: PrimitiveDateTime) -> Self {
            Self {
                year: value.year() as u16,
                month: u8::from(value.month()) as u16,
                day: value.day() as u16,
                hour: value.hour() as u16,
                minute: value.minute() as u16,
                second: value.second() as u16,
                milliseconds: value.millisecond(),
                ..Default::default()
            }
        }
        fn time(self) -> Option<PrimitiveDateTime> {
            Some(PrimitiveDateTime::new(
                Date::from_calendar_date(
                    i32::from(self.year),
                    Month::try_from(self.month as u8).ok()?,
                    self.day as u8,
                )
                .ok()?,
                Time::from_hms_milli(
                    self.hour as u8,
                    self.minute as u8,
                    self.second as u8,
                    self.milliseconds,
                )
                .ok()?,
            ))
        }
    }
    #[repr(C)]
    #[derive(Default)]
    struct Zone {
        bias: i32,
        standard_name: [u16; 32],
        standard_date: SystemTime,
        standard_bias: i32,
        daylight_name: [u16; 32],
        daylight_date: SystemTime,
        daylight_bias: i32,
    }
    #[repr(C)]
    struct DynamicZone {
        zone: Zone,
        key_name: [u16; 128],
        daylight_disabled: u8,
    }
    #[link(name = "kernel32")]
    unsafe extern "system" {
        fn GetTimeZoneInformation(zone: *mut Zone) -> u32;
        fn GetDynamicTimeZoneInformation(zone: *mut DynamicZone) -> u32;
        fn SystemTimeToTzSpecificLocalTime(
            zone: *const Zone,
            utc: *const SystemTime,
            local: *mut SystemTime,
        ) -> i32;
    }
    pub fn matches_zone(id: &str) -> bool {
        let mut zone = DynamicZone {
            zone: Zone::default(),
            key_name: [0; 128],
            daylight_disabled: 0,
        };
        // SAFETY: repr(C) matches DYNAMIC_TIME_ZONE_INFORMATION, and the initialized output remains live.
        if unsafe { GetDynamicTimeZoneInformation(&mut zone) } == u32::MAX {
            return false;
        }
        let end = zone
            .key_name
            .iter()
            .position(|c| *c == 0)
            .unwrap_or(zone.key_name.len());
        String::from_utf16(&zone.key_name[..end]).is_ok_and(|name| name == id)
    }
    fn zone() -> Option<Zone> {
        let mut zone = Zone::default();
        // SAFETY: correctly aligned TIME_ZONE_INFORMATION output lives throughout the call.
        (unsafe { GetTimeZoneInformation(&mut zone) } != u32::MAX).then_some(zone)
    }
    fn in_zone(value: OffsetDateTime, zone: &Zone) -> Option<PrimitiveDateTime> {
        let value = value.to_offset(time::UtcOffset::UTC);
        let utc = SystemTime::from_time(PrimitiveDateTime::new(value.date(), value.time()));
        let mut local = SystemTime::default();
        // SAFETY: all three repr(C) inputs/outputs are valid for this synchronous read-only clock conversion.
        if unsafe { SystemTimeToTzSpecificLocalTime(zone, &utc, &mut local) } == 0 {
            return None;
        }
        local.time()
    }
    pub fn local(value: OffsetDateTime) -> Option<PrimitiveDateTime> {
        in_zone(value, &zone()?)
    }
    pub fn utc(local: PrimitiveDateTime) -> Option<OffsetDateTime> {
        let zone = zone()?;
        resolve(
            local,
            [
                zone.bias.checked_add(zone.standard_bias)?,
                zone.bias.checked_add(zone.daylight_bias)?,
            ],
            |t| in_zone(t, &zone),
        )
    }
}

#[cfg(windows)]
pub use native::{local, matches_zone, utc};
#[cfg(not(windows))]
pub fn matches_zone(_: &str) -> bool {
    false
}
#[cfg(not(windows))]
pub fn local(_: OffsetDateTime) -> Option<PrimitiveDateTime> {
    None
}
#[cfg(not(windows))]
pub fn utc(_: PrimitiveDateTime) -> Option<OffsetDateTime> {
    None
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn ambiguous_and_nonexistent_local_clocks_are_unavailable() {
        let local = PrimitiveDateTime::new(
            time::Date::from_calendar_date(2026, time::Month::October, 25).unwrap(),
            time::Time::from_hms(3, 30, 0).unwrap(),
        );
        assert!(resolve(local, [-120, -180], |_| Some(local)).is_none());
        assert!(resolve(local, [-120, -180], |_| None).is_none());
        let expected = local.assume_utc() - time::Duration::minutes(120);
        assert_eq!(
            resolve(local, [-120, -180], |value| (value == expected)
                .then_some(local)),
            Some(expected)
        );
        assert_eq!(
            resolve(local, [-120, -120], |_| Some(local)),
            Some(expected)
        );
    }
    #[cfg(windows)]
    #[test]
    fn windows_clock_abi_and_current_time_roundtrip() {
        let now = OffsetDateTime::now_utc().replace_nanosecond(0).unwrap();
        let roundtrip = local(now).and_then(utc);
        // The current instant may itself be in the repeated DST hour: failing closed is allowed.
        assert!(roundtrip.is_none() || roundtrip == Some(now));
    }
}
