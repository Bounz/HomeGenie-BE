#!/bin/sh

_cwd="$PWD"

hg_revision_number=$1 # version like 1.1.17
hg_target_folder=$2   # temp dir to create package like /opt/buildagent/work/6ec76f1fb1fe664d
hg_archive=$3         # archive name like V1.1.17.644

hg_location=/usr/local/bin/homegenie


if [ -d "${hg_target_folder}" ]
then

    hg_target_folder="${hg_target_folder}/HGBE_${hg_revision_number}_all"
	mkdir -p "$hg_target_folder$hg_location"

	echo "\n- Extracting archive files to '$hg_target_folder'..."
    unzip ../../${hg_archive}.zip -d ${hg_target_folder}${hg_location}

	echo "\n- Cleaning '$hg_target_folder'..."
	rm -rf "$hg_target_folder$hg_location/log"
	rm -rf "$hg_target_folder$hg_location/libCameraCaptureV4L.so"
	rm -rf "$hg_target_folder$hg_location/liblirc_client.so"
	rm -rf "$hg_target_folder$hg_location/libusb-1.0.so"

	echo "\n- Generating md5sums in DEBIAN folder..."
	cd ${hg_target_folder}
	find . -type f ! -regex '.*.hg.*' ! -regex '.*?debian-binary.*' ! -regex '.*?DEBIAN.*' -printf '%P ' | xargs md5sum > ${_cwd}/DEBIAN/md5sums

	hg_installed_size=`du -s ./usr | cut -f1`
	echo "  installed size: $hg_installed_size"
	cd "$_cwd"

	echo "- Copying updated DEBIAN folder..."
	cp -r ./DEBIAN "$hg_target_folder/"
	sed -i s/_version_/${hg_revision_number}/g "$hg_target_folder/DEBIAN/control"
	sed -i s/_size_/${hg_installed_size}/g "$hg_target_folder/DEBIAN/control"

	echo "- Fixing permissions..."
	chmod -R 755 "$hg_target_folder/DEBIAN"

	echo "\n- Building deb file...\n"
	dpkg-deb --build "$hg_target_folder"

	rm -rf "$hg_target_folder"
	echo "\n... done!\n"
    
else

    echo "Error: Directory '$hg_target_folder' does not exists."

fi
