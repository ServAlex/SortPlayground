using LargeFileSort.FileDeletion;
using LargeFileSort.FileGeneration;
using LargeFileSort.FileSorting;

namespace LargeFileSort;

public class ApplicationRunner(FileGenerator fileGenerator, FileSorter fileSorter, LeftoversRemover leftoversRemover)
{
	public void Run()
	{
		fileGenerator.GenerateFile();
		fileSorter.SortFile();
		leftoversRemover.Remove();
	}
}