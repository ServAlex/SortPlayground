
# Large file sort


## All in one - generate random file and sort:
```
cd LargeFileSort
dotnet run -c Release --generate true --sizeGb 10 --path ../SortTemp --sort true
```
### Only generate random file:
```
dotnet run -c Release --generate true --sizeGb 10 --path ../SortTemp
```
### Only sort:
```
dotnet run -c Release --sort true --path ../SortTemp
```


## Delete files created by the previous run:
```
dotnet run -c Release --delete true --path ../SortTemp
```

## Note: 
Running as `dotnet run -c Release` is significantly more performant than
running in Release mode in Rider, around 30% faster.

---

## Available Options:

```
--generate            - bool, generate the random file, default: false
--reuseUnsorted       - bool, reuse random file at path if size matches, default: true
--sizeGb              - int, size of the file to be generated, default: 10

--sort                - bool, sort unsorted file, default: false
--reuseChunks         - bool, reuse partially sorted chunks if exist, default: false
--chunkFileSizeMb     - int, default: 1024
--baseChunkSizeMb     - int, size of chunk sorted directly, default: 63

--path                - string, default: ./SortArtifacts
--delete              - bool, delete all created files, overrides keepChunks, default: false
--keepChunks          - bool, keep chunks after run, default: true
--memoryBudgetGb      - int, default: 16
```