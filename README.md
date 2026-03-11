
# Large file sort

# TOC:

<a href="#how-to-run">How to run</a>
<a href="#all-in-one---generate-random-file-and-sort">Run sort</a>
<a href="#delete-files-created-by-the-previous-run">Delete created files</a>
<a href="#available-options">Cli options</a>

<a href="#run-time-mesurements">Run time mesurements</a>

<a href="#problem-description">Problem description</a>

<a href="#algorithm">Algorithm</a>

<a href="#improvements-and-notes">Improvements and Notes</a>


# How to run:
Run in command line inside `LargeFileSort` project directory using following commands:

## All in one - generate random file and sort:
```
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
Running as `dotnet run -c Release` in command line is significantly more performant than
running in Release mode in Rider, around 30-40% faster.


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


# Run time mesurements:
Run time on my machine (12 threads, 16gb memory budget, sata ssd, 1gb intermediate files):

| File size | File generation<br/>time | Split<br/>time | Merge<br/>time | Sort total time<br/>(split+merge) |
|-----------|--------------------------|----------------|----------------|-----------------------------------|
| 10 gb     | 21s                      | 43s            | 46s            | 89s                               |
| 20 gb     | 45s                      | 87s            | 97s            | 184s                              |
| 40 gb     | 88s                      | 175s           | 238s           | 413s                              |
| 100 gb    | 210s                     | 441s           | 632s           | 1083s                             |


# Problem description:

Generate large text file with random data. Lines have format:

`<Number>. <String>`

Sort file by string part, if strings match - sort by number.

According to clarification, RAM budget is 16gb, string part is up to 100 characters long.

# Algorithm:
File is sorted using merge sort in 2 steps:
1. Split step:
    1) Split unsorted file into chunks (readChunkSize size, 32mb default).
    2) Sort each chunk.
    3) Merge into large chunks (chunkFileSize size, 1024mb default).
    4) Write as intermediate files.


2. Merge step:
    1) Group intermediate files.
    2) Each group is merged using priority queue.
    3) Results of all the groups are merged into final file with another priority queue.

All the parts of Split step run in parallel, 1.2) and 1.3) have more than one worker.

Merge step: lets say we have 25 intermediate files, they will be grouped into 5 groups.
Each group runs on separate thread and merges 5 files. Another thread merges results of 5 groups into file.


# Improvements and Notes:

- Improve test coverage.
- Try parallel reading of unsorted file.
- For smaller files that fit in RAM have a separate routine without intermediate files.
- Power profile on the laptop makes a lot of difference: balanced is around 10% slower than performance, power saing is 2 times slower than performance.