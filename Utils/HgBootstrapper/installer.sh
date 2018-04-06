#!/bin/bash
default_install_directory=/usr/local/bin/hgbe

#Docker
if [ -x "$(command -v docker)" ]; then
    echo "Docker is already installed"
else
    echo "Install docker"
    #curl -sSL https://get.docker.com | sh
    wget -qO- https://get.docker.com | sh
    echo "Docker intallation complete"
fi

#HomeGenie
read -p "Enter installation path [${default_install_directory}]: " install_directory
install_directory=${install_directory:-${default_install_directory}}
echo "Installing HomeGenie (Bounz Edition) into $install_directory"

mkdir -p $install_directory
mkdir -p $install_directory/service

downloadSource=https://raw.githubusercontent.com/Bounz/HomeGenie-BE/master/Utils/HgBootstrapper
#curl -o $install_directory/service/start.sh ${downloadSource}/start.sh
#curl -o $install_directory/service/stop.sh ${downloadSource}/stop.sh
#curl -o /etc/systemd/system/hgbe.service ${downloadSource}/hgbe.svc
wget -O $install_directory/service/start.sh ${downloadSource}/start.sh
wget -O $install_directory/service/stop.sh ${downloadSource}/stop.sh
wget -O /etc/systemd/system/hgbe.service ${downloadSource}/hgbe.svc

sed -i '' "s#__install_directory__#${install_directory}#g" /etc/systemd/system/hgbe.service

systemctl enable hgbe
systemctl start hgbe
