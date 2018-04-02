FROM jetbrains/teamcity-agent:latest
LABEL maintainer="Alexander Sidorenko <me@bounz.net>"
LABEL description="TeamCity build agent image with mono, github-release and hashdeep"

#RUN apt-get update

ENV MONO_VERSION 5.8.0.127
RUN apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF

RUN echo "deb http://download.mono-project.com/repo/debian stable-jessie/snapshots/$MONO_VERSION main" > /etc/apt/sources.list.d/mono-official-stable.list \
  && apt-get update \
  && apt-get install -y \
    mono-complete \
    hashdeep \
  && rm -rf /var/lib/apt/lists/* /tmp/*

RUN curl -O -L https://github.com/aktau/github-release/releases/download/v0.7.2/linux-amd64-github-release.tar.bz2
RUN tar -xjvf linux-amd64-github-release.tar.bz2 && mv bin/linux/amd64/github-release /opt/github-release
