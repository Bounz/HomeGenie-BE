# Testing

# Clear-Host
# $env:APPVEYOR_BUILD_VERSION = "1.1.123"
# $env:configuration = "debug"
# $env:APPVEYOR_BUILD_FOLDER = "c:\projects\homegenie"

#---------------------------------#
#         create deb file         #
#---------------------------------#

$checkoutFolder = "$($env:APPVEYOR_BUILD_FOLDER)"
$controlFile = "$checkoutFolder\HomeGenie_Linux\Packager\DEBIAN\control"

# Create folder for packaging from
New-Item $checkoutFolder\release -type directory -force | out-null

# Copy output from the build
# Todo - get this working via a variable and pass into compiler command above
if (!$env:CONFIGURATION) { $env:CONFIGURATION = "debug" }

Copy-Item -Path $checkoutFolder\HomeGenie\bin\$($env:CONFIGURATION)\ -Destination $checkoutFolder\release\usr\local\bin\homegenie -recurse -force

# prevent md5sums error
remove-item $checkoutFolder\HomeGenie_Linux\Packager\DEBIAN\md5sums -errorAction SilentlyContinue

# Remove items
remove-item $checkoutFolder\release\log -force -recurse -errorAction SilentlyContinue |out-null
get-childitem -path $checkoutFolder\release -filter libCameraCaptureV4L.so -Recurse | remove-item |out-null
get-childitem -path $checkoutFolder\release -filter liblirc_client.so -Recurse | remove-item |out-null
get-childitem -path $checkoutFolder\release -filter libusb-1.0.so -Recurse | remove-item |out-null

# Get the folder size in Kb and trim.. Consider Math::Ceiling to round
$Size = [Math]::Ceiling((get-childitem -path "$checkoutFolder\release\usr" -recurse | Measure-Object -property length -sum).Sum / 1Kb)

# Replace _version_ and _size_ placeholders in control file
(Get-Content $controlFile) -replace "_version_", "$($env:APPVEYOR_BUILD_VERSION)" | out-file $controlFile -encoding ASCII -force
(Get-Content $controlFile) -replace "_size_", $Size | out-file $controlFile -encoding ASCII -force

# Copy the files needed to make a .deb package
Copy-Item -Path $checkoutFolder\HomeGenie_Linux\Packager\DEBIAN -Destination $checkoutFolder\release -recurse -force 
Copy-Item -Path $checkoutFolder\HomeGenie_Linux\Packager\DEBIAN -Destination $checkoutFolder\release\usr\local\bin\homegenie -recurse -force

# Call wpkg to create .deb package
. C:\WPKG\bin\wpkg.exe --build $checkoutFolder\release --output-dir $checkoutFolder\release --verbose
