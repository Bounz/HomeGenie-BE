#!/bin/bash
LG='\033[1;32m' # Light green
YL='\033[1;33m' # Yellow
RED='\033[0;31m' # Red
NC='\033[0m'     # No Color
default_install_directory=/usr/local/bin/hgbe

#Docker
if [ -x "$(command -v docker)" ]; then
    echo "${LG}Docker is already installed${NC}"
else
    echo "${YL}Install docker${NC}"
    #curl -sSL https://get.docker.com | sh
    wget -qO- https://get.docker.com | sh
    echo "${LG}Docker intallation complete${NC}\n"
fi

#HomeGenie
echo "Enter installation path [${default_install_directory}]: "
read install_directory
install_directory=${install_directory:-${default_install_directory}}
echo "Installing HomeGenie (Bounz Edition) into $install_directory"

sudo mkdir -p $install_directory
sudo mkdir -p $install_directory/service

downloadSource=https://raw.githubusercontent.com/Bounz/HomeGenie-BE/master/Utils/HgBootstrapper
#curl -o $install_directory/service/start.sh ${downloadSource}/start.sh
#curl -o $install_directory/service/stop.sh ${downloadSource}/stop.sh
#curl -o /etc/systemd/system/hgbe.service ${downloadSource}/hgbe.svc
wget -qO $install_directory/service/start.sh ${downloadSource}/start.sh
wget -qO $install_directory/service/stop.sh ${downloadSource}/stop.sh
wget -qO /etc/systemd/system/hgbe.service ${downloadSource}/hgbe.svc

sudo sed -i "s#__install_directory__#${install_directory}#g" /etc/systemd/system/hgbe.service

sudo systemctl enable hgbe
sudo systemctl start hgbe

echo "${LG}HomeGenie (Bounz Edition) successfully installed${NC}"
echo "Use following commands to start/stop service:"
echo "${YL}sudo systemctl start hgbe${NC}"
echo "${YL}sudo systemctl stop hgbe${NC}"
