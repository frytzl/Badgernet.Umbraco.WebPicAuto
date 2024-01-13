# Badgernet.Umbraco.WebPicAuto

This package automatically converts uploaded pictures into .webp format. It also downscales images to desired size. 


# Installation
Simply add the package by using donet add package to install the latest version:
```
dotnet add package Badgernet.Umbraco.WebPicAuto
```

# Settings
You can change these default settings by adding the section to the appsettings.json file and overwrite the values.

```
  "WebPicAuto": {
    "WpaEnableResizing": true,
    "WpaEnableConverting": true,
    "WpaConvertMode": "lossy",
    "WpaConvertQuality": 80,
    "WpaTargetWidth": 1920,
    "WpaTargetHeight": 1080,
    "WpaKeepOriginals": false,
    "WpaIgnoreKeyword": "wpaignore_"
  }
```

#### Enable/disable resizing. 

```
"WpaEnableResizing": true
```
#### Enable/disable converting to .webp format

```
"WpaEnableConverting": true
```
#### Convert mode
Type of format conversion, possible values are "lossy" and "lossless"
```
"WpaConvertMode": "lossy"
```

#### Convert quality
Valid values are from 1 to 100
```
"WpaConvertQuality": 80
```

#### Target width in px
```
""WpaTargetWidth": 1920
```

#### Target height in px
```
"WpaTargetHeight": 1080
```

#### Flag if orignal images should be kept
```
"WpaKeepOriginals": false
```
#### Ignore keyword
Any images containing this keyword in their name will be ignored by this package. -> "wpaignore_IMG01012024.png" will not get processed.
```
"WpaIgnoreKeyword": "wpaignore"
```

# Credits
This project was inspired by [@VirjdagOnline.ImageResizer]
