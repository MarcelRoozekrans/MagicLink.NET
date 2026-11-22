# The MagicLink.NET container image (issue #12): the host in src/MagicLink.NET.Host, published as a Native AOT binary on
# a chiseled base image without a shell or package manager. Build it from the repository root:
#
#   docker build -t magiclink-net .
#
# Every setting comes from environment variables; see "Running it from the container image" in README.md.

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble-aot AS build
WORKDIR /source
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig README.md ./
COPY src/ src/
RUN dotnet publish src/MagicLink.NET.Host -c Release -r linux-x64 -o /app

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app/MagicLink.NET.Host ./
# The base image runs as a user without root rights and listens on port 8080; see ASPNETCORE_HTTP_PORTS.
EXPOSE 8080
ENTRYPOINT ["./MagicLink.NET.Host"]
