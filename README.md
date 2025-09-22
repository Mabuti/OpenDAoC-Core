# OpenDAoC
[![Build and Release](https://github.com/OpenDAoC/OpenDAoC-Core/actions/workflows/build-and-release.yml/badge.svg)](https://github.com/OpenDAoC/OpenDAoC-Core/actions/workflows/build-and-release.yml)

## About

OpenDAoC is an emulator for Dark Age of Camelot (DAoC) servers, originally a fork of the [DOLSharp](https://github.com/Dawn-of-Light/DOLSharp) project.

Now completely rewritten with ECS architecture, OpenDAoC ensures performance and scalability for many players, providing a robust platform for creating and managing DAoC servers.

While the project focuses on recreating the DAoC 1.65 experience, it can be adapted for any patch level.

## Documentation

The easiest way to get started with OpenDAoC is to use Docker. Check out the `docker-compose.yml` file in the repository root for an example setup.

For detailed instructions and additional setup options, refer to the full [OpenDAoC Documentation](https://www.opendaoc.com/docs/).

## Installing the .NET SDK locally

Some workflows—such as running tests with `dotnet test`—require the .NET CLI to be available on your machine. The repository includes a helper script that downloads the required SDK without needing global installation rights. Run the following from the repository root:

```bash
./scripts/install-dotnet.sh
```

By default the script installs the latest .NET 9 SDK into `.dotnet/` inside the repository. To target a specific version, pass it as the first argument, for example:

```bash
./scripts/install-dotnet.sh 9.0.100
```

After the installation completes, add the SDK to your shell `PATH`:

```bash
export DOTNET_ROOT="$(pwd)/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
```

You can confirm that the CLI is available with `dotnet --info`.

## Releases

Releases for OpenDAoC are available at [OpenDAoC Releases](https://github.com/OpenDAoC/OpenDAoC-Core/releases).

OpenDAoC is also available as a Docker image, which can be pulled from the following registries:

- [GitHub Container Registry](https://ghcr.io/opendaoc/opendaoc-core) (recommended): `ghcr.io/opendaoc/opendaoc-core/opendaoc:latest`
- [Docker Hub](https://hub.docker.com/repository/docker/claitz/opendaoc/): `claitz/opendaoc:latest`

For detailed instructions and additional setup options, refer to the documentation.

## Companion Repositories

Several companion repositories are part of the [OpenDAoC project](https://github.com/OpenDAoC).

Some of the main repositories include:

- [OpenDAoC Database v1.65](https://github.com/OpenDAoC/OpenDAoC-Database)
- [Account Manager](https://github.com/OpenDAoC/opendaoc-accountmanager)
- [Client Launcher](https://github.com/OpenDAoC/OpenDAoC-Launcher)

## License

OpenDAoC is licensed under the [GNU General Public License (GPL)](https://choosealicense.com/licenses/gpl-3.0/) v3 to serve the DAoC community and promote open-source development.  
See the [LICENSE](LICENSE) file for more details.
