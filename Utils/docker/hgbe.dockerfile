ARG arch
FROM bounz/hgbe.base:latest-${arch}
LABEL maintainer="Alexander Sidorenko <me@bounz.net>"

COPY ./hg_compiled /usr/local/bin/homegenie/bin/
RUN chmod -R 777 /usr/local/bin/homegenie
CMD ["/usr/bin/mono" , "/usr/local/bin/homegenie/bin/HomeGenie.exe"] 

ENV HGBE_DOCKER 1
EXPOSE 80
VOLUME /usr/local/bin/homegenie/data
VOLUME /usr/local/bin/homegenie/logs
