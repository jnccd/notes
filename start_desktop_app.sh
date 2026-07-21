#!/usr/bin/env bash
cd ./NotesAvalonia.Desktop

if [ -z "$NIXOS_JNCCD_GUI_STARTER_UNCHANGED" ] || [ "$NIXOS_JNCCD_GUI_STARTER_UNCHANGED" != "1" ]; then
  dotnet run -c Release
else
  dotnet ./bin/Release/net9.0/NotesAvalonia.dll
fi
