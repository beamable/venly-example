namespace Beamable.Server;

/// <summary>
/// This class represents the existence of the VenlyFederationStorage database.
/// Use it for type safe access to the database.
/// <code>
/// var db = await Storage.GetDatabase&lt;VenlyFederationStorage&gt;();
/// </code>
/// </summary>
[StorageObject("VenlyFederationStorage")]
public class VenlyFederationStorage : MongoStorageObject
{
		
}