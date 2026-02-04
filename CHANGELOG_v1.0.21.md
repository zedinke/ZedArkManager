# Release v1.0.21

## Changes

- Added Docker Setup button to each server card
- Created DockerSetupWindow for editing docker-compose.yml files
- Docker Setup window allows editing all docker-compose settings:
  - Boolean switches (BATTLEEYE, API, RCON_ENABLED, etc.)
  - MOTD and MOTD_DURATION
  - Map name selection (with custom map option)
  - Session name, server admin password, server password
  - Max players
  - Cluster ID (read-only)
  - Mod IDs and Passive Mods
  - Custom server args
  - Memory limit
- Docker Setup window automatically loads existing docker-compose.yml values
- Docker Setup window preserves ports and cluster paths when saving
- Added localization strings for Docker Setup feature

## Technical Details

- Created `DockerSetupWindow.xaml` and `DockerSetupWindow.xaml.cs`
- Added `DockerSetupCommand` to `ServerCardViewModel`
- Added Docker Setup button to `ServerCard.xaml` (4-column grid layout)
- Implemented YAML parsing to extract environment variables from docker-compose.yml
- Implemented YAML generation to create updated docker-compose.yml file
- Ports and cluster paths are extracted from original file and preserved during save
