using System.Dynamic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NotEnoughLogs;

namespace Bunkum.Core.Configuration;

/// <summary>
/// A custom configuration file for a Bunkum server.
/// If you're looking for Bunkum's base configuration, see <see cref="BunkumConfig"/>.
/// </summary>
[JsonObject(MemberSerialization.OptOut)]
public abstract class Config
{
    [JsonIgnore]
    public string? Filename { get; private set; }

    /// <summary>
    /// A number representing the schema version of your configuration. Start at 1, then change it when you make a change to your config class.
    /// </summary>
    [JsonIgnore]
    public abstract int CurrentConfigVersion { get; }
    
    /// <summary>
    /// The configuration's current schema version. Do not touch this - you do not need to.
    /// </summary>
    public abstract int Version { get; set; }

    /// <summary>
    /// Migrate a configuration. Called when the config is loaded and the current version is newer than the one in the configuration.
    /// </summary>
    /// <param name="oldVer">The version of the configuration we are migrating.</param>
    /// <param name="oldConfig">The configuration we are migrating, stored as dynamic.</param>
    protected internal abstract void Migrate(int oldVer, dynamic oldConfig);

    /// <summary>
    /// Will be called immediately after <see cref="Migrate"/> if you have passed an IEnumerable of Configs to <see cref="LoadFromJsonFile"/>
    /// via its otherConfigs parameter. Override this method and pass your desired configs into <see cref="LoadFromJsonFile"/> if you want to apply
    /// additional migrations to this config that involve other configs, e.g. if you want to move a config option into another config,
    /// or you need to read another config's option to migrate one of this config's own options.
    /// Be aware that the base method does nothing, and nothing in the base Config class will take care of saving the configs in the parameter,
    /// so if you do edit any of them, you will have to save them yourself using their now public <see cref="SaveToJsonFile"/> method.
    /// </summary>
    /// <param name="oldVer">The version of the configuration we are migrating.</param>
    /// <param name="oldConfig">The configuration we are migrating, stored as dynamic.</param>
    /// <param name="otherConfigs">The list of other configs you have passed, which you want to use during migration.</param>
    protected internal virtual void MigrateUsingOtherConfigs(int oldVer, dynamic oldConfig, IEnumerable<Config> otherConfigs)
    {
        // Do nothing if not overridden
    }

    [JsonIgnore]
    private bool NeedsMigration => this.CurrentConfigVersion > this.Version;

    private void MigrateIfNecessary(string? jsonText = null, IEnumerable<Config>? otherConfigs = null)
    {
        if (!this.NeedsMigration) return;
        
        dynamic? configDynamic;
        if (jsonText != null)
        {
            configDynamic = JsonConvert.DeserializeObject<ExpandoObject>(jsonText, new ExpandoObjectConverter());
            if (configDynamic == null) throw new ArgumentNullException(nameof(configDynamic));
        }
        else
        {
            configDynamic = this;
        }
            
        this.Migrate(this.Version, configDynamic);
        if (otherConfigs != null)
        {
            this.MigrateUsingOtherConfigs(this.Version, configDynamic, otherConfigs);
        }
        this.Version = this.CurrentConfigVersion;
        
        // Save config to disk if possible
        this.SaveToJsonFile();
    }

    public void SaveToJsonFile()
    {
        if (this.Filename == null) throw new InvalidOperationException($"Cannot save config '{this.GetType().Name}' to JSON file because it's not loaded (filename is not set)."
            + $" Please use {nameof(LoadFromJsonFile)}() first, then try to save the config.");
        
        string json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(this.Filename, json);
    }

    /// <summary>
    /// Loads or creates a configuration from a .json file on disk.
    /// </summary>
    /// <param name="filename">What the config's filename should be stored as</param>
    /// <param name="logger">An instance of the logger, used for debugging</param>
    /// <typeparam name="TConfig">An object extending <see cref="Config"/> that represents your server's configuration.</typeparam>
    /// <returns>The configuration, either freshly created or previously existing.</returns>
    /// <exception cref="ArgumentNullException">The file could not be found or created.</exception>
    public static TConfig LoadFromJsonFile<TConfig>(string filename, Logger logger, IEnumerable<Config>? otherConfigs = null) where TConfig : Config, new()
    {
        TConfig? config;
        string? file = null;

        filename = Path.Combine(BunkumFileSystem.DataDirectory, filename);
        
        if (File.Exists(filename))
        {
            file = File.ReadAllText(filename);
            config = JsonConvert.DeserializeObject<TConfig>(file);
        }
        else
        {
            logger.LogInfo(BunkumCategory.Configuration, $"A new {typeof(TConfig).Name} is being created at {Path.GetFullPath(filename)}.");
            config = new TConfig();
        }
        
        if (config == null) throw new ArgumentNullException(nameof(filename));

        config.Filename = filename;
        config.MigrateIfNecessary(file, otherConfigs);

        return config;
    }
}