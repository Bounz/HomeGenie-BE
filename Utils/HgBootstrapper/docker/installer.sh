#!/bin/bash
LG='\033[1;32m' # Light green
YL='\033[1;33m' # Yellow
RED='\033[0;31m' # Red
NC='\033[0m'     # No Color
default_install_directory=/usr/local/bin/hgbe
image_and_tag="bounz/homegenie:latest"

command_exists() {
	command -v "$@" > /dev/null 2>&1
}

sh_c='sh -c'
if [ "$user" != 'root' ]; then
	if command_exists sudo; then
		sh_c='sudo -E sh -c'
	elif command_exists su; then
		sh_c='su -c'
	else
		cat >&2 <<-'EOF'
		Error: this installer needs the ability to run commands as root.
		We are unable to find either "sudo" or "su" available to make this happen.
		EOF
		exit 1
	fi
fi

# if is_dry_run; then
# 	sh_c="echo"
# fi

echo "_  _ ____ _  _ ____ ____ ____ _  _ _ ____    ___  ____ _  _ _  _ ___     ____ ___  _ ___ _ ____ _  _ ";
echo "|__| |  | |\/| |___ | __ |___ |\ | | |___    |__] |  | |  | |\ |   /     |___ |  \ |  |  | |  | |\ | ";
echo "|  | |__| |  | |___ |__] |___ | \| | |___    |__] |__| |__| | \|  /__    |___ |__/ |  |  | |__| | \| ";
echo "                                                                                                     ";

#Docker
if [ -x "$(command -v docker)" ]; then
    echo -e "${LG}Docker is already installed${NC}"
else
    echo -e "${YL}Install docker${NC}"
    wget -qO- https://get.docker.com | sh
    echo -e "${LG}Docker intallation complete${NC}\n"
fi

#HomeGenie
echo "Enter installation path [${default_install_directory}]: "
read install_directory </dev/tty
install_directory=${install_directory:-${default_install_directory}}
echo "Installing HomeGenie (Bounz Edition) into $install_directory"

$sh_c "mkdir -p $install_directory"
$sh_c "mkdir -p $install_directory/service"

downloadSource=https://raw.githubusercontent.com/Bounz/HomeGenie-BE/master/Utils/HgBootstrapper/docker
$sh_c "wget -qO $install_directory/service/start.sh ${downloadSource}/start.sh"
$sh_c "wget -qO $install_directory/service/stop.sh ${downloadSource}/stop.sh"
$sh_c "wget -qO /etc/systemd/system/hgbe.service ${downloadSource}/hgbe.svc"

$sh_c "sed -i \"s#__install_directory__#${install_directory}#g\" /etc/systemd/system/hgbe.service"

$sh_c "systemctl enable hgbe"
$sh_c "systemctl start hgbe"

$sh_c "docker pull ${image_and_tag}"

echo -e "${LG}HomeGenie (Bounz Edition) successfully installed${NC}"
echo "Use following commands to start/stop service:"
echo -e "    ${YL}sudo systemctl start hgbe${NC}"
echo -e "    ${YL}sudo systemctl stop hgbe${NC}"
