# CryCompressor
Cross-platform CLI tool for batch compressing media files using FFmpeg.

## What is this for?
For archiving large amounts of media files by compressing them to save space by using multiple encoders concurrently and better utilizing extra CPU/GPU resources on a system.

## How it works?
It goes through all files inside a given input directory and attempts to compress them to the output directory while retaining the input directory structure. 
If compression fails, the file is simply copied over and the error is logged.

The whole process is made to be more efficient by using priority parameters lists for FFmpeg:
```json
"ParametersPriorityList": [
  {
    "Parameters": "-c:v hevc_nvenc -rc:v constqp -cq:v 26 -preset slow -c:a aac -b:a 256k -f mp4",
    "Extension": "mp4"
  },
  {
    "Parameters": "-c:v libx265 -crf 26 -preset medium -c:a aac -b:a 256k -f mp4",
    "Extension": "mp4"
  }
]
```
This is useful when using more than 1 allowed concurrent workers, the workers will use parameters from this list in the given order. 
The first worker will take the first one, the second worker the second one, any extra workers take the last parameters.

By doing this, we can utilize both GPU and CPU at the same time. In the above example, 
one worker will be using the NVENC encoder that uses the GPU for encoding, while other workers will use the CPU-based encoder.

## Usage
This program will look for a `compressor-config.jsonc` configuration file and attempt to load it. If no such file is a found, a new one is generated automatically from a template.

Adjust the configuration as needed and simply run the program again when ready.

## Requirements
- FFmpeg

## Example configuration
Assuming we have a system with a powerful GPU and CPU, we could allocate one worker to GPU, and another 2 workers to the CPU.

Example configuration:
```jsonc
{
  "InputDirectory": "path/to/input/dir",
  "OutputDirectory": "path/to/output/dir",
  "VideoCompression": {
    "CompressVideos": true,
    "MinSize": 100000,
    "MaxConcurrentWorkers": 3,
    "RandomSuffixOnDifferentExtension": true,
    "ParametersPriorityList": [
      {
        "Parameters": "-c:v hevc_nvenc -rc:v constqp -cq:v 26 -preset slow -c:a aac -b:a 256k -f mp4",
        "Extension": "mp4"
      },
      {
        "Parameters": "-c:v libx265 -crf 26 -preset medium -c:a aac -b:a 256k -f mp4",
        "Extension": "mp4"
      }
    ]
  },
  "ImageCompression": {
    "CompressImages": true,
    "MinSize": 30000,
    "MaxConcurrentWorkers": 4,
    "RandomSuffixOnDifferentExtension": true,
    "ParametersPriorityList": [
      {
        "Parameters": "-c:v libwebp -qscale 90",
        "Extension": "webp"
      }
    ]
  },
  "AudioCompression": {
    "CompressAudio": true,
    "MinSize": 30000,
    "MaxConcurrentWorkers": 4,
    "RandomSuffixOnDifferentExtension": true,
    "ParametersPriorityList": [
      {
        "Parameters": "-c:a libopus -b:a 256k -vn",
        "Extension": "ogg"
      }
    ]
  },
  "VideoExtensions": [
    "mp4",
    "mpg",
    "mts",
    "mov",
    "avi",
    "wmv",
    "webm",
    "flv",
    "mpeg",
    "mpv"
  ],
  "ImageExtensions": [
    "jpg",
    "jpeg",
    "png",
    "bmp"
  ],
  "AudioExtensions": [
    "wav",
    "ogg",
    "oga",
    "wma",
    "mp3",
    "aac",
    "flac",
    "m4a"
  ],
  "IgnoredVideoCodecs": [
    "h265",
    "hevc",
    "vp9",
    "av1"
  ],
  "DeleteResultIfBigger": true
}
```
