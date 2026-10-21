using Gelyn.Core.Internals;

// CA1515 reads the declared accessibility of the types an extension block generates without walking up to
// their internal container, so it reports as public API what nothing outside the assembly can reach. The
// diagnostic has no source location, leaving module scope as the only suppression available.
[assembly: SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = Justifications.ByDesign, Scope = "module")]
