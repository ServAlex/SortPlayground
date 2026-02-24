namespace LargeFileSort.Common;

public sealed class InvalidConfigurationException(string message) : Exception(message);

// distinguished from system InsufficientMemoryException
public sealed class InsufficientFreeMemoryException(string message) : Exception(message);

public sealed class InsufficientFreeDiskException(string message) : Exception(message);
