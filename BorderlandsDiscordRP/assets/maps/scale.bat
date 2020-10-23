@echo off
mkdir out
for %%i in (raw/*) do magick raw/%%i -resize 512x512 out/%%i