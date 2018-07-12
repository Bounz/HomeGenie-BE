default_install_directory=/usr/local/bin/hgbe

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

# installing utils
if ! command_exists shtool; then
    echo "Installing shtool..."
    $sh_c "apt install -y shtool"
fi
if ! command_exists unzip; then
    echo "Installing unzip..."
    $sh_c "apt install -y unzip"
fi

# installing mono
DISTR=$(shtool platform --format %{sp})
echo "Installing mono on $DISTR"

case $DISTR in
    Ubuntu\ 18\.04)
        $sh_c "apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF"
        echo "deb https://download.mono-project.com/repo/ubuntu stable-bionic main" | $sh_c "tee" /etc/apt/sources.list.d/mono-official-stable.list
        $sh_c "apt update"
        ;;
      
    Ubuntu\ 16\.04)
        $sh_c "apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF"
        $sh_c "apt install apt-transport-https"
        echo "deb https://download.mono-project.com/repo/ubuntu stable-xenial main" | $sh_c "tee" /etc/apt/sources.list.d/mono-official-stable.list
        $sh_c "apt update"
        ;;
      
    Ubuntu\ 14\.04)
        $sh_c "apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF"
        $sh_c "apt install apt-transport-https"
        echo "deb https://download.mono-project.com/repo/ubuntu stable-trusty main" | $sh_c "tee" /etc/apt/sources.list.d/mono-official-stable.list
        $sh_c "apt update"
        ;;

    Debian\ 9)
        $sh_c "apt install apt-transport-https dirmngr"
        $sh_c "apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF"
        echo "deb https://download.mono-project.com/repo/debian stable-stretch main" | $sh_c "tee" /etc/apt/sources.list.d/mono-official-stable.list
        $sh_c "apt update"
        ;;

    Debian\ 8)
        $sh_c "apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF"
        $sh_c "apt install apt-transport-https"
        echo "deb https://download.mono-project.com/repo/debian stable-jessie main" | $sh_c "tee" /etc/apt/sources.list.d/mono-official-stable.list
        $sh_c "apt update"
        ;;

    Raspbian\ 9)
        $sh_c "apt install apt-transport-https dirmngr"
        $sh_c "apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF"
        echo "deb https://download.mono-project.com/repo/debian stable-raspbianstretch main" | $sh_c "tee" /etc/apt/sources.list.d/mono-official-stable.list
        $sh_c "apt update"
        ;;

    Raspbian\ 8)
        $sh_c "apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF"
        $sh_c "apt install apt-transport-https"
        echo "deb https://download.mono-project.com/repo/debian stable-raspbianjessie main" | $sh_c "tee" /etc/apt/sources.list.d/mono-official-stable.list
        $sh_c "apt update"
        ;;
    
    # CentOS\ 7)
    #     rpm --import "https://keyserver.ubuntu.com/pks/lookup?op=get&search=0x3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF"
    #     su -c 'curl https://download.mono-project.com/repo/centos7-stable.repo | tee /etc/yum.repos.d/mono-centos7-stable.repo'
    #     ;;
      
    # CentOS\ 6)
    #     srpm --import "https://keyserver.ubuntu.com/pks/lookup?op=get&search=0x3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF"
    #     su -c 'curl https://download.mono-project.com/repo/centos6-stable.repo | tee /etc/yum.repos.d/mono-centos6-stable.repo'
    #     ;;
      
    *)
        echo "Unknown OS $DISTR"
        exit 1 
esac

case $DISTR in
    Ubuntu*|Debian*|Raspbian*)
        $sh_c "apt install -y mono-complete"
        ;;
    
    # CentOS*)
    #     yum install -y mono-devel
    #     ;;
      
    *)
        echo "Unknown OS $DISTR"
        exit 1 
esac

mono_bin=$(which mono)

#HomeGenie
echo ""
echo "Enter installation path for HomeGenie [${default_install_directory}]: "
read install_directory </dev/tty
install_directory=${install_directory:-${default_install_directory}}
echo "Installing HomeGenie (Bounz Edition) into $install_directory"

$sh_c "mkdir -p $install_directory"
$sh_c "mkdir -p $install_directory/bin"
$sh_c "mkdir -p $install_directory/service"

#Downloading and unpacking the latest stable release
archive_url=$(curl -s https://api.github.com/repos/Bounz/HomeGenie-BE/releases/latest | grep 'browser_.*.zip' | cut -d\" -f4)
archive_name=$(echo $archive_url | cut -d/ -f9)
echo "Archive URL: $archive_url"
$sh_c "wget -q $archive_url"
$sh_c "unzip -o $archive_name -d $install_directory/bin"

#Setting up service
downloadSource=https://raw.githubusercontent.com/Bounz/HomeGenie-BE/master/Utils/HgBootstrapper/direct
$sh_c "wget -qO /etc/systemd/system/hgbe.service ${downloadSource}/hgbe.svc"

$sh_c "sed -i \"s#__install_directory__#${install_directory}#g\" /etc/systemd/system/hgbe.service"
$sh_c "sed -i \"s#__mono_bin__#${mono_bin}#g\" /etc/systemd/system/hgbe.service"

$sh_c "systemctl enable hgbe"
$sh_c "systemctl start hgbe"

echo -e "${LG}HomeGenie (Bounz Edition) successfully installed${NC}"
echo "Use following commands to start/stop service:"
echo -e "    ${YL}sudo systemctl start hgbe${NC}"
echo -e "    ${YL}sudo systemctl stop hgbe${NC}"
