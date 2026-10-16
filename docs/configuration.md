# Configuring AnglePlates

Most projects don't configure anything: add the package, put templates in a `Templates` folder, and build. This page covers the MSBuild side for when that's not enough, such as templates in other folders, settings for a whole repository, templates other assemblies can use, and libraries you pack. [Writing templates](templates.md) and [Rendering templates](rendering.md) cover the templates themselves.

## Settings at a glance

| Setting | Kind | Default | What it does |
| --- | --- | --- | --- |
| `EnableDefaultAnglePlateItems` | Property | `true` | `false` turns off the default `Templates` folder. |
| `AnglePlate` | Item | `Templates\**\*` | The files AnglePlates checks for templates. |
| `AnglePlateBaseAccessModifier` | Property | `internal` | The access modifier of the generated support types: `public` or `internal`. |
| `RootNamespace` | Property | The project name | The start of every namespace AnglePlates derives for a template without `#namespace`. |

## The Templates folder

The package's props file adds every file under the project's `Templates` folder and its subfolders as an `AnglePlate` item, whatever its extension. A file that starts with a directive becomes a template. Anything else, like a `README.md`, a `.gitkeep`, or an image, is skipped quietly.

The folder name is case-sensitive on Linux and macOS, so a `templates` folder works on Windows and silently generates nothing on a Linux build server. Name it `Templates`.

AnglePlates only reads the files it tagged itself. Other tools' `AdditionalFiles`, like a `PublicAPI.Shipped.txt`, never reach the template checks.

## Turning the default folder off

Set `EnableDefaultAnglePlateItems` to `false` and the props file doesn't add the folder at all:

```xml
<PropertyGroup>
  <EnableDefaultAnglePlateItems>false</EnableDefaultAnglePlateItems>
</PropertyGroup>
```

MSBuild evaluates every property before any item, so this works in your project file and in `Directory.Build.props`, where it turns the folder off for a whole repository. If your project's `Templates` folder belongs to some other tool, set it to `false`, then add your templates explicitly.

## Adding and removing files

Add templates from anywhere with `Include`:

```xml
<ItemGroup>
  <AnglePlate Include="Emails\**\*.html" />
  <AnglePlate Include="..\Shared\receipt.html" />
</ItemGroup>
```

Files you add yourself have to be templates. If one doesn't start with a directive, the build reports [`SLAP008`](diagnostics.md#slap008), so keep your wildcards narrow. A file that's in the default folder and also listed with an `Include` counts as one you added, and a file matched twice is still only read once.

A template outside the project folder can't get a derived namespace, so `..\Shared\receipt.html` needs its own `#namespace`. Without one, the derived namespace isn't valid C#, and the compiler reports an error in the generated file.

To leave out a few files and keep the rest of the default folder, remove them:

```xml
<ItemGroup>
  <AnglePlate Remove="Templates\drafts\**\*" />
</ItemGroup>
```

## `Directory.Build.props` or `Directory.Build.targets`

MSBuild imports `Directory.Build.props` before your project file and the package's props, and `Directory.Build.targets` after them. Properties don't care about that order, but `Remove` does: it only removes items that already exist.

| Where | `EnableDefaultAnglePlateItems` | `AnglePlate Include` | `AnglePlate Remove` of a default file |
| --- | --- | --- | --- |
| Project file | Works | Works | Works |
| `Directory.Build.props` | Works | Works | Does nothing |
| `Directory.Build.targets` | Works; the props file is the usual place | Works | Works |

A `Remove` in `Directory.Build.props` runs before the package adds the default folder, so there's nothing to remove yet. Use the property there, or move the `Remove` to `Directory.Build.targets`.

## Public templates

AnglePlates generates `TemplateOptions`, `TextTemplate<TModel>`, `Utf8Template<TModel>`, and `TemplateExtensions` into every project that uses it. They're `internal` until you set `AnglePlateBaseAccessModifier` to `public`, and so are your compiled templates until their `#access` directive says `public`. Most templates only get used by the project they live in, so that's usually all you need.

If another assembly needs your templates, make the support types public:

```xml
<PropertyGroup>
  <AnglePlateBaseAccessModifier>public</AnglePlateBaseAccessModifier>
</PropertyGroup>
```

Then add `#access public` to each template you want to expose. Templates without it stay `internal`. The other way around doesn't work: a `#access public` template on top of `internal` support types reports [`SLAP005`](diagnostics.md#slap005) and doesn't generate, because a public class can't expose internal base types.

The property takes `public` or `internal`, in any casing and with or without surrounding spaces. Anything else reports [`SLAP009`](diagnostics.md#slap009), and AnglePlates generates nothing for the project until you fix it. I'd rather fail the build than guess at the accessibility of your base classes.

## Sharing templates between assemblies

The support types always use the `SlusserLabs.AnglePlates` namespace. A project that can see another assembly's copies, because they're `public` or shared through `InternalsVisibleTo`, ends up with two of everything:

- If that project uses AnglePlates too, it gets `CS0436` warnings, because its own copies win over the imported ones. It can still render the other assembly's templates, but it can't pass its own `TemplateOptions` to them, so use their `Default` instances.
- If it doesn't use AnglePlates, and it sees copies from two assemblies, it gets `CS0433` errors wherever it names `TemplateOptions`, because the name is ambiguous.

The first case is harmless once you know it's coming, so a project like that can keep warnings as errors and exempt `CS0436`:

```xml
<PropertyGroup>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <WarningsNotAsErrors>$(WarningsNotAsErrors);CS0436</WarningsNotAsErrors>
</PropertyGroup>
```

## Libraries and `PrivateAssets`

The package's props and targets only apply to the project that references AnglePlates. A project that references your library doesn't get template discovery from it, and its own `Templates` folder isn't touched unless it references AnglePlates itself.

AnglePlates is a development dependency, so `dotnet package add` writes the reference with `PrivateAssets` for you:

```xml
<PackageReference Include="SlusserLabs.AnglePlates" Version="1.1.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

Keep `PrivateAssets` if you write the reference by hand, especially in a library you pack. Without it, `dotnet pack` lists AnglePlates as a dependency of your package, and everyone who installs yours gets AnglePlates too.

Without `PrivateAssets`, the generator also flows to projects that reference your library, but the props and targets don't. The generator sees that the props weren't imported and adds nothing to those projects: no template classes, no support types, and no warnings.

## Central Package Management

With Central Package Management, a `GlobalPackageReference` in `Directory.Packages.props` adds AnglePlates to every project under that folder, and it's private to each one, so no project file has to name it:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <GlobalPackageReference Include="SlusserLabs.AnglePlates" Version="1.1.0" />
  </ItemGroup>
</Project>
```

Every project that gets the package gets the support types too, even one without templates. They're `internal`, so nothing outside the project sees them. If you'd rather name AnglePlates only where you use it, put a `PackageVersion` in `Directory.Packages.props` and a `PackageReference` with `PrivateAssets="all"` in those projects.
