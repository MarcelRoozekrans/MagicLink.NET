# The MagicLink.NET container image (issue #12): the host in src/MagicLink.NET.Host, published as a Native AOT binary on
# a chiseled base image without a shell or package manager. Build it from the repository root:
#
#   docker build -t magiclink-net .
#
# It builds for the platform it runs on, linux/amd64 or linux/arm64. Native AOT does not cross-compile between them, so
# each image is built on a machine of its own architecture; CI does that on GitHub's x64 and arm64 runners.
#
# Every setting comes from environment variables; see "Running it from the container image" in README.md.

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble-aot AS build
# Set by BuildKit to the architecture of the image being built.
ARG TARGETARCH
WORKDIR /source
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig README.md ./
COPY src/ src/
RUN case "$TARGETARCH" in \
      amd64) rid=linux-x64 ;; \
      arm64) rid=linux-arm64 ;; \
      *) echo "No Native AOT build for architecture '$TARGETARCH'." >&2; exit 1 ;; \
    esac \
    && dotnet publish src/MagicLink.NET.Host -c Release -r "$rid" -o /app

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app/MagicLink.NET.Host ./
# The base image runs as a user without root rights and listens on port 8080; see ASPNETCORE_HTTP_PORTS.
EXPOSE 8080
ENTRYPOINT ["./MagicLink.NET.Host"]
