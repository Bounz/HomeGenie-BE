ARG arch
FROM ${arch}/mono
LABEL maintainer="Alexander Sidorenko <me@bounz.net>"

RUN apt-get update && apt-get install -y \
  unzip \
  alsa-utils lame \
  lirc liblircclient-dev \
  libv4l-0 \
  libusb-1.0-0 libusb-1.0-0-dev \
  arduino-mk empty-expect \
  sudo

# cleanup 
RUN rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*
