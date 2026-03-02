
# Large file sort


## All in one - generate random file and sort:
```
cd LargeFileSort
dotnet run -c Release --generate true --fileSize 10gb --path ./SortTemp/ --sort true
```
### Only generate random file:
```
dotnet run -c Release --generate true --fileSize 10gb --path ./SortTemp
```
### Only sort existing file:
```
dotnet run -c Release --sort true --path ./SortTemp
```


## Delete files created by the previous run:
```
dotnet run -c Release --delete true --path ./SortTemp
```

## Note: 
Running as `dotnet run -c Release` is significantly more performant than
running in Release mode in Rider, around 30% faster.

---

## Available Options:

```
--generate         - bool,   generate the random file, default: false
--reuseUnsorted    - bool,   reuse random file at path if size matches, default: true
--fileSize         - size,   file size to be generated, (Ex: 512mb, 1gb), default: 10gb

--sort             - bool,   sort unsorted file, default: false
--reuseChunks      - bool,   reuse partially sorted chunks if exist, default: false
--chunkFileSize    - size,   default: 1024mb
--readChunkSize    - size,   size of chunk read and sorted directly, default: 32mb

--path             - string, default: ./SortArtifacts
--delete           - bool,   delete all created files, overrides keepChunks, default: false
--keepChunks       - bool,   keep chunks after run, default: true
--memoryBudget     - size,   default: 16gb

Note: data size is expressed as a whole number or a number with suffix kb|mb|gb, 
      example: 1024, 1mb, 10gb

```