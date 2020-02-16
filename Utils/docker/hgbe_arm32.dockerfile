FROM multiarch/qemu-user-static:x86_64-arm as qemu
FROM arm32v7/mono
COPY --from=qemu /usr/bin/qemu-arm-static /usr/bin

LABEL maintainer="Alexander Sidorenko <me@bounz.net>"

#COPY ./qemu-arm-static /usr/bin/qemu-arm-static

# RUN apt-get update && apt-get install -y \
#   unzip \
#   alsa-utils lame \
#   lirc liblircclient-dev \
#   libv4l-0 \
#   libusb-1.0-0 libusb-1.0-0-dev \
#   arduino-mk empty-expect \
#   sudo

COPY ./hg_compiled /usr/local/bin/homegenie/bin/

# cleanup 
RUN rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*
RUN chmod -R 777 /usr/local/bin/homegenie
#CMD ["/usr/bin/mono" , "/usr/local/bin/homegenie/bin/HomeGenie.exe"] 
CMD ["sh", "-c", "uname -m & ls -al"] 

ENV HGBE_DOCKER 1
EXPOSE 80
VOLUME /usr/local/bin/homegenie/data
VOLUME /usr/local/bin/homegenie/logs
