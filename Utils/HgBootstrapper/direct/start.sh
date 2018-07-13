#!/bin/sh

MONO=__mono_bin__

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

EXITCODE="1"
while [ "$EXITCODE" = "1" ]; do
    $sh_c "$MONO HomeGenie.exe"
    EXITCODE="$?"
    echo "Exit code: $EXITCODE"
done
