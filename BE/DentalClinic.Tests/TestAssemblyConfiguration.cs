using Xunit;

// Disable parallel test execution across test classes in DentalClinic.Tests
// because integration tests share real database resources (e.g. dentists, chairs) on SQL Server.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
