#!/bin/bash
cont_name="homegenie_be"
image_and_tag="bounz/hgbe:latest"
install_directory="__install_directory__"

# Проверить и при необходимости создать папки для HG
if [ ! -d ${install_directory}/data ]; then
  mkdir -p ${install_directory}/data
fi
if [ ! -d ${install_directory}/logs ]; then
  mkdir -p ${install_directory}/logs
fi

# При определенном коде завершения - 1 - перезапустить контейнер
# При определенном коде завершения - 5 - удалить контейнер, скачать новый образ, запустить контейнер
exit_code=1
host_ip=`ip route get 1 | awk '{print $NF;exit}'`
echo "Host IP: $host_ip"

while [ "${exit_code}" -ne 0 ]
do

  # При запуске проверить, есть ли контейнер HG в docker
  # Если есть - запустить его, если нет - скачать и запустить
  cont_id=`docker ps -q -f name=${cont_name}`
  echo "1st check: $cont_id"
  if [ -z "${cont_id}" ]; then
    cont_id=`docker ps -aq -f status=exited -f name=${cont_name}`
    echo 2nd check: $cont_id
    if [ -n "$cont_id" ]; then
      echo "Found existing container ${cont_name}, starting it..."
      cont_id=`docker start ${cont_name}`
    else
      # run your container
      echo "No existing container ${cont_name}, running it..."
      docker run -d --privileged \
        --name ${cont_name} \
        --network="host" \
        -v /etc/localtime:/etc/localtime:ro \
        -v ${install_directory}/data:/usr/local/bin/homegenie/data \
        -v ${install_directory}/logs:/usr/local/bin/homegenie/logs \
        ${image_and_tag}
    fi
  fi

  # При запуске контейнера отслеживать его выход
  exit_code=`docker wait ${cont_name}`
  echo "Container ${cont_name} exited with code ${exit_code}"

  if [ "${exit_code}" == 5 ]; then
    echo "Removing container ${cont_name}..."
    docker container rm ${cont_name}
    echo "Removing image ${image_and_tag}..."
    docker image rm ${image_and_tag}
  fi

  sleep 2
done

echo "Good bye!"
