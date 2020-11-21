#!/bin/sh
go build -buildmode=c-shared -o OSBindingsV2.dll -ldflags="-extldflags=-Wl,--version-script=$(pwd)/.version -s -w"