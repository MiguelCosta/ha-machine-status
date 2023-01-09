# ha-machine-status

## Publish & Run

```bash
dotnet publish --use-current-runtime --sc --version-suffix 1.0.0.1 -o output/
```

```bash
sudo mv output/* /opt/ha-machine-status
```

## Run as a service

- Create file: /usr/lib/systemd/system/ha-machine-status.service

```
[Unit]
Description=HA MachineStatus Worker
After=network.target

[Service]
Type=exec
ExecStart=/opt/ha-machine-status/HAMachineStatusWorker
WorkingDirectory=/opt/ha-machine-status
Restart=always

[Install]
WantedBy=default.target
```

Run:

```
sudo systemctl start ha-machine-status.service
sudo systemctl status ha-machine-status.service
sudo systemctl stop ha-machine-status.service
```
