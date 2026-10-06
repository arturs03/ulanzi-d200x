#![forbid(unsafe_code)]

use d200x_provider_protocol::{self as protocol, RequestKind, Response, Sample, Unit};
use std::{
    fs,
    io::{self, Write},
    time::Duration,
};

fn run() -> io::Result<()> {
    // Failure-mode configuration belongs to this purpose-built test executable only.
    let mode = fs::read_to_string("fixture-mode.txt").unwrap_or_else(|_| "normal".to_owned());
    let mode = mode.trim();
    fs::write("fixture-started.txt", std::process::id().to_string())?;
    let mut reader = io::stdin().lock();
    let mut writer = io::stdout().lock();
    let mut previous = 0;
    while let Some(frame) = protocol::read_frame(&mut reader)? {
        let request = protocol::parse_request(&frame, previous)?;
        previous = request.request_id;
        if mode == "exit" {
            return Ok(());
        }
        if mode == "hang-hello"
            || (mode == "hang-snapshot" && request.kind == RequestKind::Snapshot)
            || (mode == "hang-shutdown" && request.kind == RequestKind::Shutdown)
        {
            std::thread::sleep(Duration::from_secs(60));
        }
        if request.kind == RequestKind::Hello {
            match mode {
                "oversized" => {
                    writer.write_all(&vec![b'x'; protocol::MAXIMUM_BYTES + 1])?;
                    writer.flush()?;
                    return Ok(());
                }
                "partial" => {
                    writer.write_all(b"{\"protocolVersion\":1")?;
                    writer.flush()?;
                    return Ok(());
                }
                "malformed" => {
                    writer.write_all(b"{bad}\n")?;
                    writer.flush()?;
                    return Ok(());
                }
                "invalid-utf8" => {
                    writer.write_all(&[255, b'\n'])?;
                    writer.flush()?;
                    return Ok(());
                }
                "stderr-flood" => {
                    io::stderr().lock().write_all(&vec![b'e'; 1_048_576])?;
                }
                "orphan" => {
                    let mut child = std::process::Command::new(std::env::current_exe()?)
                        .arg("child")
                        .spawn()?;
                    fs::write("fixture-child.txt", child.id().to_string())?;
                    // Host job owns termination; waiting here would prevent the exit test.
                    let _ = child.try_wait()?;
                    return Ok(());
                }
                _ => {}
            }
        }
        let mut response = Response {
            protocol_version: if mode == "wrong-version" {
                2
            } else {
                protocol::VERSION
            },
            request_id: if mode == "wrong-id" {
                request.request_id + 1
            } else {
                request.request_id
            },
            kind: request.kind,
            provider_id: None,
            samples: None,
        };
        if request.kind == RequestKind::Hello {
            response.provider_id = Some(
                if mode == "wrong-identity" {
                    "wrong.provider"
                } else {
                    "d200x.fixture"
                }
                .to_owned(),
            );
        } else if request.kind == RequestKind::Snapshot {
            let selected = request
                .selections
                .as_deref()
                .ok_or_else(|| io::Error::other("missing selections"))?;
            let timestamp = if mode == "stale" {
                "2000-01-01T00:00:00Z".to_owned()
            } else if mode == "future" {
                "2099-01-01T00:00:00Z".to_owned()
            } else {
                protocol::now()?
            };
            response.samples = Some(
                selected
                    .iter()
                    .map(|selection| {
                        Sample::reading(
                            selection,
                            Unit::Percent,
                            if mode == "invalid-value" { 101.0 } else { 42.0 },
                            &timestamp,
                        )
                    })
                    .collect(),
            );
        }
        protocol::write_response(&mut writer, &response)?;
        if mode == "duplicate" {
            protocol::write_response(&mut writer, &response)?;
        }
        if request.kind == RequestKind::Shutdown {
            return Ok(());
        }
    }
    Ok(())
}

fn main() {
    if std::env::args().nth(1).as_deref() == Some("child") {
        std::thread::sleep(Duration::from_secs(60));
        return;
    }
    if run().is_err() {
        eprintln!("fixture-failed");
        std::process::exit(1);
    }
}
