FROM mono:5.8.0.127
LABEL maintainer="Alexander Sidorenko <me@bounz.net>"

# RUN apt-get update && apt-get install -y \
# 	sudo \
# 	gdebi-core \
# 	libusb-1.0-0 \
# 	libusb-1.0-0-dev \
# 	usbutils \
# 	alsa-utils \
# 	lame \
# 	lirc --no-install-recommends \
# 	libv4l-0 \
# 	apt-utils \
# 	mc

RUN apt-get update

# Audio playback utilities
RUN apt-get install -y \
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
ADD https://github.com/Bounz/HomeGenie-BE/releases/download/V1.1.16.5/HGBE_V1.1.16.5.zip /tmp/
# RUN gdebi --non-interactive /tmp/homegenie-beta_1.1.r525_all.deb
RUN unzip /tmp/HGBE_V1.1.16.5.zip -d /usr/local/bin
# cleanup 
RUN rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*
RUN chmod -R 777 /usr/local/bin/homegenie
RUN chmod +x /usr/local/bin/homegenie/startup.sh
CMD ["usr/local/bin/homegenie/startup.sh", "/usr/local/bin/homegenie"]
#CMD ["/usr/bin/mono" , "/usr/local/bin/homegenie/HomeGenie.exe"] 
#ENTRYPOINT ["/bin/bash"] 
EXPOSE 8080 80
