
# Large file sort

How to run:

`dotnet run -c Release --generate true --sizeGb 10 --path ../SortTemp --sort true`

Run and delete all the created files afterward:

`dotnet run -c Release --generate true --sizeGb 10 --path ../SortTemp --sort true --delete true`

Delete files created by the previous run:

`dotnet run -c Release --delete true --path ../SortTemp`

---

Available Options:

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