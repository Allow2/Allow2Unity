#!/bin/sh -eu

Configuration="Release"
if [ $# -lt 1 ]; then
    echo "Need path to Unity"
    exit 1
fi

if [ $# -gt 1 ]; then
    case x"$2" in
        xdebug | xDebug)
            Configuration="Debug"
            ;;
    esac
fi

pushd unity/PackageProject/Assets
git clean -xdf
popd

pushd src
git clean -xdf
popd

OS="Mac"
if [ -e "c:\\" ]; then
    OS="Windows"
fi

if [ x"$OS" == x"Windows" ]; then
    if [ -f "$1/Editor/Unity.exe" ]; then
        Unity="$1/Editor/Unity.exe"
    else
        echo "Can't find Unity in $1"
        exit 1
    fi
else
    if [ -f "$1/Unity.app/Contents/MacOS/Unity" ]; then
        Unity="$1/Unity.app/Contents/MacOS/Unity"
    elif [ -f "$1/Unity" ]; then
        Unity="$1/Unity"
    else
        echo "Can't find Unity in $1"
        exit 1
    fi
fi

if [ x"$OS" == x"Windows" ]; then
    common/nuget restore Allow2.sln
else
    nuget restore Allow2.sln
fi

xbuild Allow2.sln /property:Configuration=$Configuration

Version=`sed -En 's,.*Version = "(.*)".*,\1,p' common/SolutionInfo.cs`
commitcount=`git rev-list  --count HEAD`
commit=`git log -n1 --pretty=format:%h`
Version="${Version}.${commitcount}-${commit}"
Version=$Version
export ALLOW22_DISABLE=1
"$Unity" -batchmode -projectPath "`pwd`/unity/PackageProject" -exportPackage Assets/Plugins/Allow2 allow2-for-unity-$Version.unitypackage -force-free -quit
