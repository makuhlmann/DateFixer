# DateFixer
This is a small utility that changes the "date modified" field of one or multiple files to either:

- ISO9660 / UDF time stamps (for image files)
- Signature timestamp (for signed files)
- Timestamp found in the file name (for any file type)

## Usage

```
DateFixer Usage: datefixer [/i] [/s] path1 [path2] [path3] ...

/i - Process image files
/s - Process signed files
/f - Try to parse dates contained in the file name
/x - Ignore file extensions, try to process all files anyway
/r - Process folders recursively
/q - Do not print all files that were touched
/? - Shows list of commands

Default options: /i /s
The file name parser is looking for various formats in the order yyyyMMdd[HHmm[ss]]
```

## Default supported file types

With the /x flag the file types can be ignored, all files will be processed the same way which may increase the processing time significantly.

### ISO time stamp

- .iso
- .img

### Signature time stamp

- .exe
- .dll
- .sys
- .efi
- .scr
- .msi
- .msu
- .appx
- .appxbundle
- .msix
- .msixbundle
- .cat
- .cab
- .js
- .vbs
- .wsf
- .ps1
- .xap

### File name timestamps

Any file type is supported.

Files will be searched for a date in the `yyyyMMdd HHmmss` order. Seconds, minutes and hours are optional. Each part of the date and time may be separated by up to one character. Examples of valid dates:

- 2001-08-24_12-00-00
- 2001-08-24_12-00
- 2001-08-24
- 20010824-120000
- 20010824-1200
- 20010824120000
- 200108241200
- 20010824

Beware that dates that use the wrong order may be processed anyway, especially if the individual parts are not separated by characters. The only validation that is done is the year being checked to start with 19, 20 or 21.