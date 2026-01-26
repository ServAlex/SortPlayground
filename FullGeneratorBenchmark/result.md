| Method                               | Mean    | Error   | StdDev  | Allocated |
|------------------------------------- |--------:|--------:|--------:|----------:|
| GenerateFileSingleThreadedLineByLine | 15.02 s | 0.193 s | 0.171 s |   4.02 MB |
| GenerateFileSingleThreadedBatched    |      NA |      NA |      NA |        NA |

| Method                            | BatchSize |     Mean | Error    | StdDev   | Allocated |
|---------------------------------- |---------- |---------:|---------:|---------:|----------:|
| GenerateFileSingleThreadedBatched | 1         | 14.745 s | 0.1159 s | 0.1084 s |   4.02 MB |
| GenerateFileSingleThreadedBatched | 2         |  8.947 s | 0.1053 s | 0.0985 s |   4.02 MB |
| GenerateFileSingleThreadedBatched | 4         |  5.823 s | 0.0863 s | 0.0765 s |   4.02 MB |
| GenerateFileSingleThreadedBatched | 8         |  4.255 s | 0.0577 s | 0.0540 s |   4.02 MB |
| GenerateFileSingleThreadedBatched | 16        |  3.524 s | 0.0347 s | 0.0324 s |   4.02 MB |
| GenerateFileSingleThreadedBatched | 32        |  3.106 s | 0.0332 s | 0.0311 s |   4.03 MB |
| GenerateFileSingleThreadedBatched | 64        |  2.875 s | 0.0431 s | 0.0403 s |   4.04 MB |
| GenerateFileSingleThreadedBatched | 128       |  2.771 s | 0.0594 s | 0.0583 s |   4.05 MB |
| GenerateFileSingleThreadedBatched | 256       |  2.719 s | 0.0388 s | 0.0363 s |   4.08 MB |
| GenerateFileSingleThreadedBatched | 512       |  2.656 s | 0.0285 s | 0.0252 s |   4.14 MB |
| GenerateFileSingleThreadedBatched | 1024      |  2.650 s | 0.0261 s | 0.0232 s |   4.26 MB |

| Method                            | BatchSize | Mean    | Error    | StdDev   | Allocated |
|---------------------------------- |---------- |--------:|---------:|---------:|----------:|
| GenerateFileSingleThreadedBatched | 512       | 2.687 s | 0.0226 s | 0.0211 s |   4.14 MB |
| GenerateFileSingleThreadedBatched | 1024      | 2.666 s | 0.0376 s | 0.0333 s |   4.26 MB |
| GenerateFileSingleThreadedBatched | 2048      | 2.641 s | 0.0375 s | 0.0350 s |   4.49 MB |
| GenerateFileSingleThreadedBatched | 4096      | 2.640 s | 0.0359 s | 0.0336 s |   4.96 MB |
| GenerateFileSingleThreadedBatched | 8192      | 2.661 s | 0.0368 s | 0.0344 s |    5.9 MB |
