FROM arm32v7/mono
LABEL maintainer="Alexander Sidorenko <me@bounz.net>"

RUN apt-get update && apt-get install -y \
  unzip \
  alsa-utils lame \
  lirc liblircclient-dev \
  libv4l-0 \
  libusb-1.0-0 libusb-1.0-0-dev \
  arduino-mk empty-expect \
  sudo

#Add HomeGenie 
#ADD https://github.com/Bounz/HomeGenie/releases/download/v1.1-beta.526.1.bounz/homegenie-beta_1.1.r526.1_all.zip /tmp/
#RUN unzip /tmp/homegenie-beta_1.1.r526.1_all.zip -d /usr/local/bin

COPY * /usr/local/bin/homegenie/bin

# cleanup 
RUN rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*
RUN chmod -R 777 /usr/local/bin/homegenie
#RUN chmod +x /usr/local/bin/homegenie/startup.sh
#CMD ["usr/local/bin/homegenie/startup.sh", "/usr/local/bin/homegenie"]
CMD ["/usr/bin/mono" , "/usr/local/bin/homegenie/HomeGenie.exe"] 

EXPOSE 80
VOLUME /usr/local/bin/homegenie/data
VOLUME /usr/local/bin/homegenie/logs
VOLUME /usr/local/bin/homegenie/plugins
