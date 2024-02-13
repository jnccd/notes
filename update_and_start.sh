#!/usr/bin/env bash

# Get some sleepy time c:
SLEEP_TIME=0
DOTNET_PATH=dotnet
while [[ $# -gt 0 ]]; do
  case $1 in
    -s|--sleep)
      SLEEP_TIME=$2
      shift
      shift
      ;;
	-dp|--dotnet-path)
      DOTNET_PATH=$2
      shift
      shift
      ;;
    -*|--*)
      echo "Unknown option $1"
      exit
      ;;
    *)
      shift
      ;;
  esac
done
sleep $SLEEP_TIME

# Wait for connection
while ! ping -c 4 google.com > /dev/null; do 
  echo "The network is not up yet"
  sleep 1
done

# Get to the right place
SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
cd $SCRIPT_DIR/NotesServer

# Work
git pull
$DOTNET_PATH restore
$DOTNET_PATH run -c Release --urls="http://*:7778;https://*:7777"

# Keep process alive
echo "Keeping process alive.."
while true; do
	sleep 1000
done
