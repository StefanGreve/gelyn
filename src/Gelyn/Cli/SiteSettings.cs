using System.CommandLine;
using System.IO.Abstractions;
using System.Text.Json;

using Gelyn.Exceptions;
using Gelyn.Model;
using Gelyn.Model.Options;
using Gelyn.Validators;

using Microsoft.Extensions.Configuration;

namespace Gelyn.Cli;

/// <summary>
///     Loads the settings of a run from the configuration file, the <c>GELYN_</c> environment variables and the
///     options that override both, and validates the result.
/// </summary>
internal sealed class SiteSettings
{
    // Prefixed, because names such as LANGUAGE, PORT and TITLE are set by desktops and hosts for their own use.
    private const string SettingVariablePrefix = "GELYN_";

    private readonly string _contentRoot;
    private readonly ParseResult _parseResult;
    private readonly IFileSystem _fileSystem;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SiteSettings"/> class.
    /// </summary>
    /// <param name="contentRoot">
    ///     The directory every relative setting is resolved against.
    /// </param>
    /// <param name="path">
    ///     The absolute path of the configuration file.
    /// </param>
    /// <param name="optional">
    ///     Indicates whether the file may be absent, in which case the defaults apply without it.
    /// </param>
    /// <param name="parseResult">
    ///     The parse of the command line, whose promoted options override the file and the variables.
    /// </param>
    /// <param name="fileSystem">
    ///     The file system the file is read through.
    /// </param>
    public SiteSettings(string contentRoot, string path, bool optional, ParseResult parseResult, IFileSystem fileSystem)
    {
        this._contentRoot = contentRoot;
        this.Path = path;
        this.Optional = optional;
        this._parseResult = parseResult;
        this._fileSystem = fileSystem;
    }

    /// <summary>
    ///     The absolute path of the configuration file, whether it exists or not.
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///     Indicates whether the file may be absent, in which case the defaults apply without it.
    /// </summary>
    public bool Optional { get; }

    /// <summary>
    ///     Loads the settings from the file, with the variables and the promoted options layered over it, and
    ///     validates the result.
    /// </summary>
    /// <returns>
    ///     The settings of the run, with every value checked.
    /// </returns>
    /// <exception cref="ConfigurationException">
    ///     A required file is missing, the file is not JSON, a value cannot be converted to the type of its
    ///     setting, or a setting fails a validation rule.
    /// </exception>
    /// <exception cref="IOException">
    ///     The file cannot be opened.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    ///     The file cannot be read.
    /// </exception>
    public SiteOptions Load()
    {
        IConfigurationRoot configuration = this.Read();
        var options = new SiteOptions();

        try
        {
            configuration.Bind(options);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConfigurationException([exception.Message], exception);
        }

        OverrideOptions.Apply(this._parseResult, options);
        IReadOnlyList<string> failures = new SiteOptionsValidator(this._contentRoot, this.Path).Validate(options);

        return failures.Count > 0
            ? throw new ConfigurationException(failures)
            : options;
    }

    /// <summary>
    ///     Reports whether the file exists and whether it can be read, without binding or validating it.
    /// </summary>
    /// <returns>
    ///     The status of the file, with the failure a read raised held in it.
    /// </returns>
    public ConfigurationFileStatus Probe()
    {
        bool exists = this._fileSystem.File.Exists(this.Path);
        Exception? failure = null;

        if (exists || !this.Optional)
        {
            try
            {
                this.Read();
            }
            catch (Exception exception) when (exception is ConfigurationException or IOException or UnauthorizedAccessException)
            {
                failure = exception;
            }
        }

        return new ConfigurationFileStatus
        {
            Path = this.Path,
            Optional = this.Optional,
            Exists = exists,
            Failure = failure,
        };
    }

    #region Helpers

    // The variables outrank the file, which is the documented order.
    private IConfigurationRoot Read()
    {
        var builder = new ConfigurationBuilder();
        using FileSystemStream? file = this.OpenFile();

        if (file is not null) builder.AddJsonStream(file);

        builder.AddEnvironmentVariables(prefix: SettingVariablePrefix);

        try
        {
            return builder.Build();
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new ConfigurationException([$"Failed to load configuration from file '{this.Path}'. {exception.Message}"], exception);
        }
    }

    // One open instead of a probe and an open, with the exception telling a missing file from one that cannot be opened.
    private FileSystemStream? OpenFile()
    {
        try
        {
            return this._fileSystem.File.OpenRead(this.Path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            if (this.Optional) return null;

            throw new ConfigurationException([$"The configuration file '{this.Path}' was not found and is not optional."], exception);
        }
    }

    #endregion
}
