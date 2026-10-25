using System;
using System.Net;
using System.Threading.Tasks;

using Gelyn.Model;
using Gelyn.Services;
using Gelyn.Tests.Fixtures;

namespace Gelyn.Tests.Services;

/// <summary>
///     Unit tests for the <see cref="StaticFileResolver"/> class.
/// </summary>
public class StaticFileResolverTests
{
    #region Directory Tests

    /// <summary>
    ///     Verifies that a directory resolves to the index it holds whether or not the request carries a
    ///     trailing slash, and that the site root is the same case.
    /// </summary>
    [Test]
    [Arguments("/", "index.html")]
    [Arguments("/blog/", "blog/index.html")]
    [Arguments("/blog", "blog/index.html")]
    [Arguments("/blog/2026/", "blog/2026/index.html")]
    public async Task Resolve_Test_Directory_ResolvesToItsIndex(string requestPath, string expected)
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html", "blog/index.html", "blog/2026/index.html");

        // Act
        ServedFile file = fixture.Resolve(requestPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(file.Path).IsEqualTo(fixture.Locate(expected));
            await Assert.That(file.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(file.ContentType).IsEqualTo("text/html; charset=utf-8");
        }
    }

    /// <summary>
    ///     Verifies that a directory holding no index is a miss and never a listing, which is what the
    ///     deploy targets answer too.
    /// </summary>
    [Test]
    public async Task Resolve_Test_DirectoryWithoutAnIndex_IsAMiss()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html", "assets/logo.png");

        // Act
        ServedFile file = fixture.Resolve("/assets/");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(file.Path).IsNull();
            await Assert.That(file.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }
    }

    #endregion // Directory Tests

    #region Extension Tests

    /// <summary>
    ///     Verifies that an extensionless path resolves to the page of that name however deep it sits,
    ///     because both GitHub Pages and Cloudflare Pages do and a preview that disagrees with the deploy
    ///     target is worse than none.
    /// </summary>
    [Test]
    [Arguments("/about", "about.html")]
    [Arguments("/blog/hello", "blog/hello.html")]
    public async Task Resolve_Test_ExtensionlessPath_ResolvesToTheHtmlFile(string requestPath, string expected)
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html", "about.html", "blog/hello.html");

        // Act
        ServedFile file = fixture.Resolve(requestPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(file.Path).IsEqualTo(fixture.Locate(expected));
            await Assert.That(file.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }
    }

    /// <summary>
    ///     Verifies that a file is served verbatim when it is requested by name, page or copy, so that the
    ///     extensionless rule is an addition and not a redirection.
    /// </summary>
    [Test]
    [Arguments("/about.html", "about.html")]
    [Arguments("/robots.txt", "robots.txt")]
    public async Task Resolve_Test_NamedFile_ResolvesToItself(string requestPath, string expected)
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html", "about.html", "robots.txt");

        // Act
        ServedFile file = fixture.Resolve(requestPath);

        // Assert
        await Assert.That(file.Path).IsEqualTo(fixture.Locate(expected));
    }

    /// <summary>
    ///     Verifies that a path already carrying an extension is never extended by another, so that a request
    ///     for a missing stylesheet cannot be answered with a page.
    /// </summary>
    [Test]
    public async Task Resolve_Test_PathCarryingAnExtension_IsNotExtended()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html", "about.css.html");

        // Act
        ServedFile file = fixture.Resolve("/about.css");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(file.Path).IsNull();
            await Assert.That(file.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }
    }

    #endregion // Extension Tests

    #region Containment Tests

    /// <summary>
    ///     Verifies that a path climbing out of the output directory is refused, however it is spelled, and
    ///     that the file it names is not served even though it exists.
    /// </summary>
    [Test]
    [Arguments("/../secret.txt")]
    [Arguments("/../../secret.txt")]
    [Arguments("/blog/../../secret.txt")]
    [Arguments("/%2e%2e/secret.txt")]
    [Arguments("/..%2fsecret.txt")]
    [Arguments("/%2e%2e%2fsecret.txt")]
    public async Task Resolve_Test_PathLeavingTheOutputDirectory_IsRefused(string requestPath)
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html", "blog/index.html").WriteBeside("secret.txt");

        // Act
        ServedFile file = fixture.Resolve(requestPath);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(file.Path).IsNull();
            await Assert.That(file.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }
    }

    /// <summary>
    ///     Verifies that an absolute path addressing a file outside the output directory is refused, which
    ///     needs an encoded separator to survive the leading one every request path carries.
    /// </summary>
    [Test]
    public async Task Resolve_Test_AbsolutePath_IsRefused()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html").WriteBeside("secret.txt");
        string outside = fixture.FileSystem.Path.Combine(fixture.Parent, "secret.txt");

        // Act
        ServedFile file = fixture.Resolve($"/{Uri.EscapeDataString(outside)}");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(file.Path).IsNull();
            await Assert.That(file.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }
    }

    /// <summary>
    ///     Verifies that a path no file system accepts is a miss and not an exception, because a request
    ///     may spell anything and the server has to survive it.
    /// </summary>
    [Test]
    public async Task Resolve_Test_UnacceptablePath_IsAMiss()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html");

        // Act
        ServedFile file = fixture.Resolve("/%00");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(file.Path).IsNull();
            await Assert.That(file.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }
    }

    /// <summary>
    ///     Verifies that an unnormalized output directory is resolved before the containment test runs, so
    ///     that a caller cannot weaken the test by passing the path it happens to hold.
    /// </summary>
    [Test]
    public async Task Resolve_Test_UnnormalizedOutputDirectory_IsResolvedFirst()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html").WriteBeside("secret.txt");
        string indirect = fixture.FileSystem.Path.Combine(fixture.Root, "blog", "..");

        // Act
        ServedFile hit = fixture.Resolver.Resolve(indirect, "/index.html");
        ServedFile miss = fixture.Resolver.Resolve(indirect, "/../secret.txt");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(hit.Path).IsEqualTo(fixture.Locate("index.html"));
            await Assert.That(miss.Path).IsNull();
        }
    }

    #endregion // Containment Tests

    #region Miss Tests

    /// <summary>
    ///     Verifies that a miss is answered with the page the site declares for one, under a status that
    ///     still reports the miss.
    /// </summary>
    [Test]
    public async Task Resolve_Test_Miss_ServesTheDeclaredNotFoundPage()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html", "404.html");

        // Act
        ServedFile file = fixture.Resolve("/nope");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(file.Path).IsEqualTo(fixture.Locate("404.html"));
            await Assert.That(file.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That(file.ContentType).IsEqualTo("text/html; charset=utf-8");
        }
    }

    /// <summary>
    ///     Verifies that the page is looked for in the output root alone, which is where the deploy targets
    ///     look for it too, and never beside the request that missed.
    /// </summary>
    [Test]
    public async Task Resolve_Test_Miss_IgnoresANestedNotFoundPage()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html", "blog/index.html", "blog/404.html");

        // Act
        ServedFile file = fixture.Resolve("/blog/nope");

        // Assert
        await Assert.That(file.Path).IsNull();
    }

    /// <summary>
    ///     Verifies that a site declaring no such page gets a bodyless 404 and not nothing at all.
    /// </summary>
    [Test]
    public async Task Resolve_Test_Miss_WithoutAPageIsBodyless()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html");

        // Act
        ServedFile file = fixture.Resolve("/nope");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(file.Path).IsNull();
            await Assert.That(file.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That(file.ContentType).IsEqualTo("text/plain; charset=utf-8");
        }
    }

    #endregion // Miss Tests

    #region Content Type Tests

    /// <summary>
    ///     Verifies that each extension the output tree can hold is served under its own media type, that the
    ///     text types declare a charset, and that an unknown extension falls back instead of guessing.
    /// </summary>
    [Test]
    [Arguments("index.html", "text/html; charset=utf-8")]
    [Arguments("styles.css", "text/css; charset=utf-8")]
    [Arguments("app.js", "text/javascript; charset=utf-8")]
    [Arguments("app.mjs", "text/javascript; charset=utf-8")]
    [Arguments("robots.txt", "text/plain; charset=utf-8")]
    [Arguments("sitemap.xml", "application/xml; charset=utf-8")]
    [Arguments("logo.svg", "image/svg+xml; charset=utf-8")]
    [Arguments("data.json", "application/json")]
    [Arguments("site.webmanifest", "application/manifest+json")]
    [Arguments("favicon.ico", "image/x-icon")]
    [Arguments("logo.png", "image/png")]
    [Arguments("photo.jpg", "image/jpeg")]
    [Arguments("photo.jpeg", "image/jpeg")]
    [Arguments("spin.gif", "image/gif")]
    [Arguments("photo.webp", "image/webp")]
    [Arguments("photo.avif", "image/avif")]
    [Arguments("text.woff", "font/woff")]
    [Arguments("text.woff2", "font/woff2")]
    [Arguments("text.ttf", "font/ttf")]
    [Arguments("text.otf", "font/otf")]
    [Arguments("paper.pdf", "application/pdf")]
    [Arguments("archive.tar.zst", "application/octet-stream")]
    [Arguments("LICENSE.unknown", "application/octet-stream")]
    public async Task Resolve_Test_ContentType_FollowsTheExtension(string relativePath, string expected)
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html", relativePath);

        // Act
        ServedFile file = fixture.Resolve($"/{relativePath}");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(file.Path).IsEqualTo(fixture.Locate(relativePath));
            await Assert.That(file.ContentType).IsEqualTo(expected);
        }
    }

    /// <summary>
    ///     Verifies that the extension is read without regard to case, because the deploy target reads it
    ///     that way and a file named by hand can carry any spelling.
    /// </summary>
    [Test]
    public async Task Resolve_Test_ContentType_DisregardsTheCaseOfTheExtension()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html", "README.HTML", "photo.JPG");

        // Act
        ServedFile page = fixture.Resolve("/README.HTML");
        ServedFile image = fixture.Resolve("/photo.JPG");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(page.ContentType).IsEqualTo("text/html; charset=utf-8");
            await Assert.That(image.ContentType).IsEqualTo("image/jpeg");
        }
    }

    #endregion // Content Type Tests
}
