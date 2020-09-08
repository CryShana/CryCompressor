# CryCompressor
Cross-platform CLI tool for batch compressing media files using FFmpeg.

## What is this for?
For archiving large amounts of media files by compressing them to save space in a way that best utilizes computer's resources.

## How it works?
It goes through all files inside a given input directory and attempts to compress them to the output directory while retaining the input directory structure. 
If compression fails, the file is simply copied over and the error is logged.

The whole process is made to be more efficient by using priority parameters lists for FFmpeg:
```json
"ParametersPriorityList": [
  {
    "Parameters": "-c:v hevc_nvenc -rc:v vbr_hq -cq:v 26 -preset slow -c:a aac -b:a 256k -f mp4",
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

## Example configuration
Assuming we have a system with a powerful GPU and CPU, we could allocate one worker to GPU, and another 2 workers to the CPU.

Example configuration:
```jsonc
{
  "InputDirectory": "C:\\inputDir",
  "OutputDirectory": "C:\\outputDir,
  
  // Video compression configuration
  "VideoCompression": {
    "CompressVideos": true,
    
    // files below this size will be ignored (given in bytes)
    "MinSize": 100000,
    
    // We will be using 3 concurrent workers
    "MaxConcurrentWorkers": 3,
    
    "ParametersPriorityList": [
      // the first worker will take these parameters
      {
        "Parameters": "-c:v hevc_nvenc -rc:v vbr_hq -cq:v 26 -preset slow -c:a aac -b:a 256k -f mp4",
        "Extension": "mp4"
      },
      // other workers will take this
      {
        "Parameters": "-c:v libx265 -crf 26 -preset medium -c:a aac -b:a 256k -f mp4",
        "Extension": "mp4"
      }
    ]
  },
  
  // Image compression configuration
  "ImageCompression": {
    "CompressImages": true,
    "MinSize": 30000,
    "MaxConcurrentWorkers": 3,
    "ParametersPriorityList": [
      // all workers will be using these parameters
      {
        "Parameters": "-c:v libwebp -qscale 90",
        "Extension": "webp"
      }
    ]
  },
  
  // Which extensions to treat as video files
  "VideoExtensions": [
    ".mp4",
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
  
  // Which extensions to treat as image files
  "ImageExtensions": [
    "jpg",
    "jpeg",
    "png",
    "bmp"
  ],
  
  // Which video codecs to ignore
  "IgnoredVideoCodecs": [
    "h265",
    "hevc",
    "vp9",
    "av1"
  ],
  
  // If the resulting compressed file turns out to be larger than the original, delete it and just copy the original over
  "DeleteResultIfBigger": true
}```
