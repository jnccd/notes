#!/usr/bin/env bash
cd ./NotesServer
dotnet ef database update
dotnet run -c Release
