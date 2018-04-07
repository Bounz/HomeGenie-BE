#FROM resin/rpi-raspbian
FROM arm32v7/mono
LABEL maintainer="Alexander Sidorenko <me@bounz.net>"

# MONO: slim install
# ENV MONO_VERSION 5.4.0.201
# RUN apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
# RUN echo "deb http://download.mono-project.com/repo/debian jessie/snapshots/$MONO_VERSION main" > /etc/apt/sources.list.d/mono-official.list \
#   && apt-get update \
#   && apt-get install -y mono-runtime \
#   && rm -rf /var/lib/apt/lists/* /tmp/*

# MONO: from slim to latest
# RUN apt-get update \
#   && apt-get install -y binutils curl mono-devel ca-certificates-mono fsharp mono-vbnc nuget referenceassemblies-pcl \
#   && rm -rf /var/lib/apt/lists/* /tmp/*

RUN apt-get update && apt-get install -y \
  unzip \
  alsa-utils lame \
  lirc liblircclient-dev \
  libv4l-0 \
  libusb-1.0-0 libusb-1.0-0-dev \
  arduino-mk empty-expect \
  sudo

# # Embedded speech syntesys engine
# RUN apt-get install libttspico-utils
# # SSL client support
# RUN apt-get install ca-certificates-mono
# # LIRC Infrared inteface
# RUN apt-get install lirc liblircclient-dev
# # Video4Linux camera
# RUN apt-get install libv4l-0
# # X10 CM15 Home Automation interface
# RUN apt-get install libusb-1.0-0 libusb-1.0-0-dev
# # Arduino™ programming from *HG* program editor
# RUN apt-get install arduino-mk empty-expect

#Add HomeGenie 
ADD https://github.com/Bounz/HomeGenie/releases/download/v1.1-beta.526.1.bounz/homegenie-beta_1.1.r526.1_all.zip /tmp/
RUN unzip /tmp/homegenie-beta_1.1.r526.1_all.zip -d /usr/local/bin

# cleanup 
RUN rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*
RUN chmod -R 777 /usr/local/bin/homegenie
#RUN chmod +x /usr/local/bin/homegenie/startup.sh
#CMD ["usr/local/bin/homegenie/startup.sh", "/usr/local/bin/homegenie"]
CMD ["/usr/bin/mono" , "/usr/local/bin/homegenie/HomeGenie.exe"] 

EXPOSE 8080 80
