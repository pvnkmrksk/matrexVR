using UnityEngine;
using UnityEditor;
using System.IO;

public class Object2Terrain : EditorWindow
{
	[MenuItem("Terrain/Object to Terrain", false, 2000)]
	static void OpenWindow()
	{
		EditorWindow window = EditorWindow.GetWindow<Object2Terrain>(true);
		window.titleContent = new GUIContent("Object to Terrain");
	}

	private int resolution = 512;
	private Vector3 addTerrain;
	int bottomTopRadioSelected = 0;
	static string[] bottomTopRadio = new string[] { "Bottom Up", "Top Down" };
	private float shiftHeight = 0f;
	private bool transferTexture = true;
	private float textureResolution = 1024f;
	private bool preserveUVs = true;
	private bool matchPosition = true;
	private bool createPrefab = true;
	private string prefabSavePath = "Assets/TerrainAssets";

	void OnGUI()
	{
		resolution = EditorGUILayout.IntField("Resolution", resolution);
		addTerrain = EditorGUILayout.Vector3Field("Add terrain", addTerrain);
		shiftHeight = EditorGUILayout.Slider("Shift height", shiftHeight, -1f, 1f);
		bottomTopRadioSelected = GUILayout.SelectionGrid(bottomTopRadioSelected, bottomTopRadio, bottomTopRadio.Length, EditorStyles.radioButton);

		matchPosition = EditorGUILayout.Toggle("Match Original Position", matchPosition);
		transferTexture = EditorGUILayout.Toggle("Transfer Texture", transferTexture);

		if (transferTexture)
		{
			textureResolution = EditorGUILayout.FloatField("Texture Resolution", textureResolution);
			preserveUVs = EditorGUILayout.Toggle("Preserve UVs", preserveUVs);
			EditorGUILayout.HelpBox("The texture transfer will sample the original mesh's texture using its UV coordinates and project it onto the terrain.", MessageType.Info);
		}

		createPrefab = EditorGUILayout.Toggle("Create Prefab Asset", createPrefab);
		if (createPrefab)
		{
			prefabSavePath = EditorGUILayout.TextField("Asset Save Path", prefabSavePath);
		}

		if (GUILayout.Button("Create Terrain"))
		{
			if (Selection.activeGameObject == null)
			{
				EditorUtility.DisplayDialog("No object selected", "Please select an object.", "Ok");
				return;
			}
			else
			{
				CreateTerrain();
			}
		}
	}

	delegate void CleanUp();

	void CreateTerrain()
	{
		//fire up the progress bar
		ShowProgressBar(1, 100);

		// Store original position and rotation for later
		GameObject sourceObject = Selection.activeGameObject;
		Vector3 originalPosition = sourceObject.transform.position;
		Quaternion originalRotation = sourceObject.transform.rotation;
		Vector3 originalScale = sourceObject.transform.localScale;

		TerrainData terrain = new TerrainData();
		terrain.heightmapResolution = resolution;
		GameObject terrainObject = Terrain.CreateTerrainGameObject(terrain);

		Undo.RegisterCreatedObjectUndo(terrainObject, "Object to Terrain");

		MeshCollider collider = sourceObject.GetComponent<MeshCollider>();
		CleanUp cleanUp = null;

		//Add a collider to our source object if it does not exist.
		//Otherwise raycasting doesn't work.
		if (!collider)
		{
			collider = sourceObject.AddComponent<MeshCollider>();
			cleanUp = () => DestroyImmediate(collider);
		}

		Bounds bounds = collider.bounds;
		float sizeFactor = collider.bounds.size.y / (collider.bounds.size.y + addTerrain.y);
		terrain.size = collider.bounds.size + addTerrain;
		bounds.size = new Vector3(terrain.size.x, collider.bounds.size.y, terrain.size.z);

		// Do raycasting samples over the object to see what terrain heights should be
		float[,] heights = new float[terrain.heightmapResolution, terrain.heightmapResolution];
		Ray ray = new Ray(new Vector3(bounds.min.x, bounds.max.y + bounds.size.y, bounds.min.z), -Vector3.up);
		RaycastHit hit = new RaycastHit();
		float meshHeightInverse = 1 / bounds.size.y;
		Vector3 rayOrigin = ray.origin;

		int maxHeight = heights.GetLength(0);
		int maxLength = heights.GetLength(1);

		Vector2 stepXZ = new Vector2(bounds.size.x / maxLength, bounds.size.z / maxHeight);

		for (int zCount = 0; zCount < maxHeight; zCount++)
		{
			ShowProgressBar(zCount, maxHeight);

			for (int xCount = 0; xCount < maxLength; xCount++)
			{
				float height = 0.0f;

				if (collider.Raycast(ray, out hit, bounds.size.y * 3))
				{
					height = (hit.point.y - bounds.min.y) * meshHeightInverse;
					height += shiftHeight;

					//bottom up
					if (bottomTopRadioSelected == 0)
					{
						height *= sizeFactor;
					}

					//clamp
					if (height < 0)
					{
						height = 0;
					}
				}

				heights[zCount, xCount] = height;
				rayOrigin.x += stepXZ[0];
				ray.origin = rayOrigin;
			}

			rayOrigin.z += stepXZ[1];
			rayOrigin.x = bounds.min.x;
			ray.origin = rayOrigin;
		}

		terrain.SetHeights(0, 0, heights);

		// Position the terrain at the original object position if requested
		if (matchPosition)
		{
			// By default terrain is centered at its position, adjust accordingly
			terrainObject.transform.position = originalPosition - new Vector3(terrain.size.x / 2, 0, terrain.size.z / 2);
		}

		// Transfer texture if enabled
		if (transferTexture)
		{
			TransferTextureToTerrain(sourceObject, terrainObject, terrain, bounds);
		}

		// Create asset/prefab if requested
		string terrainAssetPath = string.Empty;
		if (createPrefab)
		{
			terrainAssetPath = CreateTerrainAsset(terrainObject, terrain);
		}

		EditorUtility.ClearProgressBar();

		if (cleanUp != null)
		{
			cleanUp();
		}

		// Select the new terrain
		Selection.activeGameObject = terrainObject;
		EditorGUIUtility.PingObject(terrainObject);

		if (!string.IsNullOrEmpty(terrainAssetPath))
		{
			EditorUtility.DisplayDialog("Terrain Asset Created",
				$"A complete terrain asset has been created at: {terrainAssetPath}\n\nThis includes all necessary textures and terrain data.", "Ok");
		}
	}

	private string CreateTerrainAsset(GameObject terrainObject, TerrainData terrainData)
	{
		// Create a complete asset that can be easily moved to other projects
		string baseFolder = prefabSavePath;
		string terrainName = $"Terrain_{System.DateTime.Now.Ticks}";
		string terrainFolder = $"{baseFolder}/{terrainName}";

		// Create necessary folders
		if (!Directory.Exists(baseFolder))
		{
			Directory.CreateDirectory(baseFolder);
		}
		if (!Directory.Exists(terrainFolder))
		{
			Directory.CreateDirectory(terrainFolder);
		}

		// Save the terrain data as an asset
		string terrainDataPath = $"{terrainFolder}/{terrainName}_TerrainData.asset";
		AssetDatabase.CreateAsset(terrainData, terrainDataPath);

		// Save any terrain layers
		if (terrainData.terrainLayers != null && terrainData.terrainLayers.Length > 0)
		{
			string layerPath = $"{terrainFolder}/Layers";
			if (!Directory.Exists(layerPath))
			{
				Directory.CreateDirectory(layerPath);
			}

			for (int i = 0; i < terrainData.terrainLayers.Length; i++)
			{
				TerrainLayer layer = terrainData.terrainLayers[i];
				if (layer != null)
				{
					string sourcePath = AssetDatabase.GetAssetPath(layer);
					if (!string.IsNullOrEmpty(sourcePath))
					{
						string destLayerPath = $"{layerPath}/Layer_{i}.terrainlayer";
						AssetDatabase.CopyAsset(sourcePath, destLayerPath);
						terrainData.terrainLayers[i] = AssetDatabase.LoadAssetAtPath<TerrainLayer>(destLayerPath);
					}
				}
			}
		}

		// Create a prefab from the terrain gameobject
		string prefabPath = $"{terrainFolder}/{terrainName}.prefab";
		GameObject prefab = PrefabUtility.SaveAsPrefabAsset(terrainObject, prefabPath);

		// Refresh the AssetDatabase
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();

		return terrainFolder;
	}

	void TransferTextureToTerrain(GameObject sourceObject, GameObject terrainObject, TerrainData terrainData, Bounds bounds)
	{
		try
		{
			Renderer renderer = sourceObject.GetComponent<Renderer>();
			if (renderer == null)
			{
				Debug.LogWarning("No renderer found on the source object.");
				return;
			}

			// Check for materials
			Material[] materials = null;
			if (renderer is MeshRenderer)
			{
				materials = renderer.sharedMaterials;
			}
			else if (renderer is SkinnedMeshRenderer)
			{
				materials = renderer.sharedMaterials;
			}

			if (materials == null || materials.Length == 0 || materials[0] == null)
			{
				Debug.LogWarning("No valid materials found on the source object.");
				return;
			}

			// If no texture is assigned but there's a color, create a texture from the color
			if (materials[0].mainTexture == null && materials[0].color != Color.white)
			{
				CreateTextureFromColor(materials[0].color, terrainData, terrainObject);
				return;
			}

			// We'll use the first material as our primary texture source
			Material primaryMaterial = materials[0];
			if (primaryMaterial.mainTexture == null)
			{
				Debug.LogWarning("No texture found in the primary material.");
				return;
			}

			Texture2D sourceTexture = primaryMaterial.mainTexture as Texture2D;
			if (sourceTexture == null)
			{
				Debug.LogWarning("Source texture is not a Texture2D.");
				return;
			}

			// Check if texture is readable
			if (!IsTextureReadable(sourceTexture))
			{
				Debug.LogWarning("Source texture is not readable. Please enable 'Read/Write Enabled' in the texture import settings.");
				sourceTexture = MakeTextureReadable(sourceTexture);
				if (sourceTexture == null)
				{
					return;
				}
			}

			// Check for normal map
			Texture2D normalMap = null;
			if (primaryMaterial.HasProperty("_BumpMap"))
			{
				normalMap = primaryMaterial.GetTexture("_BumpMap") as Texture2D;
				if (normalMap != null && !IsTextureReadable(normalMap))
				{
					normalMap = MakeTextureReadable(normalMap);
				}
			}

			// Get mesh data for proper UV mapping
			MeshFilter meshFilter = sourceObject.GetComponent<MeshFilter>();
			if (meshFilter == null || meshFilter.sharedMesh == null)
			{
				// Try SkinnedMeshRenderer if MeshFilter is not found
				SkinnedMeshRenderer smr = sourceObject.GetComponent<SkinnedMeshRenderer>();
				if (smr != null && smr.sharedMesh != null)
				{
					Mesh tempMesh = new Mesh();
					smr.BakeMesh(tempMesh);
					meshFilter = new GameObject("TempMeshFilter", typeof(MeshFilter)).GetComponent<MeshFilter>();
					meshFilter.mesh = tempMesh;
				}
				else
				{
					Debug.LogWarning("No mesh found on the source object.");
					return;
				}
			}

			Mesh mesh = meshFilter.sharedMesh;
			Vector2[] uvs = mesh.uv;
			int[] triangles = mesh.triangles;

			if (uvs.Length == 0)
			{
				Debug.LogWarning("Mesh has no UV data. Using top-down projection instead.");
				preserveUVs = false;
			}

			// Create terrain layer
			TerrainLayer terrainLayer = new TerrainLayer();
			terrainLayer.diffuseTexture = sourceTexture;
			terrainLayer.tileSize = new Vector2(bounds.size.x, bounds.size.z);
			terrainLayer.tileOffset = Vector2.zero;

			// Set normal map if available
			if (normalMap != null)
			{
				terrainLayer.normalMapTexture = normalMap;
			}

			// Save the terrain layer asset
			string layerPath = $"{prefabSavePath}/TerrainLayers";
			if (!Directory.Exists(layerPath))
			{
				Directory.CreateDirectory(layerPath);
			}

			string assetPath = $"{layerPath}/TerrainLayer_{System.DateTime.Now.Ticks}.terrainlayer";
			AssetDatabase.CreateAsset(terrainLayer, assetPath);
			AssetDatabase.SaveAssets();

			// Set terrain layers
			terrainData.terrainLayers = new TerrainLayer[] { terrainLayer };

			// Calculate alphamap resolution based on texture resolution
			int alphamapResolution = Mathf.Max(512, Mathf.RoundToInt(textureResolution));
			terrainData.alphamapResolution = alphamapResolution;

			// Create a custom splatmap texture to better preserve the original texture
			Texture2D splatmap = new Texture2D(alphamapResolution, alphamapResolution, TextureFormat.RGBA32, false);

			if (preserveUVs)
			{
				// Use the UV-based projection method
				ProjectTextureWithUVs(sourceObject, mesh, materials, uvs, triangles, bounds, terrainData, splatmap);
			}
			else
			{
				// Use a simpler top-down projection
				ProjectTextureTopDown(sourceObject, mesh, materials, sourceTexture, bounds, terrainData, splatmap);
			}

			// Apply changes to the texture
			splatmap.Apply();

			// Save the splatmap texture as an asset
			string texturePath = $"{prefabSavePath}/TerrainTextures";
			if (!Directory.Exists(texturePath))
			{
				Directory.CreateDirectory(texturePath);
			}

			string splatmapPath = $"{texturePath}/Splatmap_{System.DateTime.Now.Ticks}.png";

			// Save texture to disk
			byte[] pngData = splatmap.EncodeToPNG();
			File.WriteAllBytes(splatmapPath, pngData);
			AssetDatabase.ImportAsset(splatmapPath);

			// Configure texture import settings
			TextureImporter textureImporter = AssetImporter.GetAtPath(splatmapPath) as TextureImporter;
			if (textureImporter != null)
			{
				textureImporter.isReadable = true;
				textureImporter.textureType = TextureImporterType.Default;
				textureImporter.wrapMode = TextureWrapMode.Clamp;
				textureImporter.filterMode = FilterMode.Bilinear;
				textureImporter.SaveAndReimport();
			}

			// Update the terrain layer with our custom texture
			Texture2D importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(splatmapPath);
			if (importedTexture != null)
			{
				terrainLayer.diffuseTexture = importedTexture;
				EditorUtility.SetDirty(terrainLayer);
				AssetDatabase.SaveAssets();
			}

			// Create alphamap (fully opaque for the single layer)
			float[,,] alphamap = new float[alphamapResolution, alphamapResolution, 1];
			for (int y = 0; y < alphamapResolution; y++)
			{
				for (int x = 0; x < alphamapResolution; x++)
				{
					alphamap[y, x, 0] = 1.0f;
				}
			}

			// Apply the alphamap to the terrain
			terrainData.SetAlphamaps(0, 0, alphamap);

			// Get the Terrain component and adjust settings
			Terrain terrainComponent = terrainObject.GetComponent<Terrain>();
			if (terrainComponent != null)
			{
				terrainComponent.materialTemplate = new Material(Shader.Find("Nature/Terrain/Standard"));
				terrainComponent.drawInstanced = true;
			}

			// Clean up any temporary objects
			if (meshFilter.gameObject.name == "TempMeshFilter")
			{
				DestroyImmediate(meshFilter.gameObject);
			}
		}
		catch (System.Exception e)
		{
			Debug.LogError("Error during texture transfer: " + e.Message + "\n" + e.StackTrace);
		}
		finally
		{
			EditorUtility.ClearProgressBar();
		}
	}

	private void CreateTextureFromColor(Color color, TerrainData terrainData, GameObject terrainObject)
	{
		// Create a simple colored texture when material has color but no texture
		Texture2D colorTexture = new Texture2D(512, 512, TextureFormat.RGBA32, false);

		for (int y = 0; y < colorTexture.height; y++)
		{
			for (int x = 0; x < colorTexture.width; x++)
			{
				colorTexture.SetPixel(x, y, color);
			}
		}
		colorTexture.Apply();

		// Save the texture
		string texturePath = $"{prefabSavePath}/TerrainTextures";
		if (!Directory.Exists(texturePath))
		{
			Directory.CreateDirectory(texturePath);
		}
		string colorTexturePath = $"{texturePath}/ColorTexture_{System.DateTime.Now.Ticks}.png";
		File.WriteAllBytes(colorTexturePath, colorTexture.EncodeToPNG());
		AssetDatabase.ImportAsset(colorTexturePath);

		// Create terrain layer with the color texture
		TerrainLayer terrainLayer = new TerrainLayer();
		terrainLayer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(colorTexturePath);
		terrainLayer.tileSize = terrainData.size;
		terrainLayer.tileOffset = Vector2.zero;

		// Save the terrain layer asset
		string layerPath = $"{prefabSavePath}/TerrainLayers";
		if (!Directory.Exists(layerPath))
		{
			Directory.CreateDirectory(layerPath);
		}
		string assetPath = $"{layerPath}/ColorLayer_{System.DateTime.Now.Ticks}.terrainlayer";
		AssetDatabase.CreateAsset(terrainLayer, assetPath);
		AssetDatabase.SaveAssets();

		// Apply to terrain
		terrainData.terrainLayers = new TerrainLayer[] { terrainLayer };

		// Create alphamap (fully opaque for the single layer)
		int alphamapResolution = 512;
		terrainData.alphamapResolution = alphamapResolution;
		float[,,] alphamap = new float[alphamapResolution, alphamapResolution, 1];
		for (int y = 0; y < alphamapResolution; y++)
		{
			for (int x = 0; x < alphamapResolution; x++)
			{
				alphamap[y, x, 0] = 1.0f;
			}
		}
		terrainData.SetAlphamaps(0, 0, alphamap);

		// Configure terrain material
		Terrain terrainComponent = terrainObject.GetComponent<Terrain>();
		if (terrainComponent != null)
		{
			terrainComponent.materialTemplate = new Material(Shader.Find("Nature/Terrain/Standard"));
			terrainComponent.drawInstanced = true;
		}
	}

	private bool IsTextureReadable(Texture2D texture)
	{
		try
		{
			texture.GetPixel(0, 0);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private Texture2D MakeTextureReadable(Texture2D source)
	{
		try
		{
			// Create a temporary readable copy
			RenderTexture tempRT = RenderTexture.GetTemporary(
				source.width, source.height, 0,
				RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

			Graphics.Blit(source, tempRT);
			RenderTexture prev = RenderTexture.active;
			RenderTexture.active = tempRT;

			Texture2D readableCopy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
			readableCopy.ReadPixels(new Rect(0, 0, tempRT.width, tempRT.height), 0, 0);
			readableCopy.Apply();

			RenderTexture.active = prev;
			RenderTexture.ReleaseTemporary(tempRT);

			return readableCopy;
		}
		catch (System.Exception e)
		{
			Debug.LogError("Error making texture readable: " + e.Message);
			return null;
		}
	}

	private void ProjectTextureWithUVs(GameObject sourceObject, Mesh mesh, Material[] materials, Vector2[] uvs, int[] triangles, Bounds bounds, TerrainData terrainData, Texture2D splatmap)
	{
		// Get the collider for raycasting
		Collider objectCollider = sourceObject.GetComponent<Collider>();
		if (objectCollider == null)
		{
			Debug.LogWarning("No collider found on source object for texture projection. Adding one temporarily.");
			objectCollider = sourceObject.AddComponent<MeshCollider>();
		}

		// Prepare for projection and sampling
		Transform objectTransform = sourceObject.transform;
		Ray ray = new Ray();
		RaycastHit hit;
		Vector3 worldPos = new Vector3();
		float terrainWidth = terrainData.size.x;
		float terrainLength = terrainData.size.z;

		// Progress tracking
		int totalPixels = splatmap.width * splatmap.height;
		int processedPixels = 0;

		// Get submesh data to identify which material to use at each point
		int[] submeshMaterialIndices = new int[mesh.subMeshCount];
		for (int i = 0; i < mesh.subMeshCount; i++)
		{
			submeshMaterialIndices[i] = i < materials.Length ? i : 0;
		}

		// We'll project from top-down to sample the texture
		for (int y = 0; y < splatmap.height; y++)
		{
			for (int x = 0; x < splatmap.width; x++)
			{
				// Update progress every 1000 pixels
				if (processedPixels % 1000 == 0)
				{
					ShowProgressBar(processedPixels, totalPixels);
				}
				processedPixels++;

				// Calculate world position for this point on the terrain
				float xPos = bounds.min.x + ((float)x / splatmap.width) * terrainWidth;
				float zPos = bounds.min.z + ((float)y / splatmap.height) * terrainLength;
				worldPos.x = xPos;
				worldPos.y = bounds.max.y + 100f; // Start ray high above
				worldPos.z = zPos;

				// Set up the ray
				ray.origin = worldPos;
				ray.direction = Vector3.down;

				Color pixelColor = Color.white; // Default color

				// Cast a ray from above to find intersection with the mesh
				if (objectCollider.Raycast(ray, out hit, float.MaxValue))
				{
					// We hit the mesh, get the triangle we hit
					int triangleIndex = hit.triangleIndex * 3;
					if (triangleIndex >= 0 && triangleIndex + 2 < triangles.Length)
					{
						// Get the indices of the triangle vertices
						int index0 = triangles[triangleIndex];
						int index1 = triangles[triangleIndex + 1];
						int index2 = triangles[triangleIndex + 2];

						// Safety check for UV indices
						if (index0 < uvs.Length && index1 < uvs.Length && index2 < uvs.Length)
						{
							// Get the UVs of the triangle vertices
							Vector2 uv0 = uvs[index0];
							Vector2 uv1 = uvs[index1];
							Vector2 uv2 = uvs[index2];

							// Calculate barycentric coordinates
							Vector3 baryCoord = hit.barycentricCoordinate;

							// Interpolate UVs using barycentric coordinates
							Vector2 interpolatedUV = uv0 * baryCoord.x + uv1 * baryCoord.y + uv2 * baryCoord.z;

							// Determine which material to use based on submesh
							int materialIndex = 0; // Default to first material
							for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
							{
								int[] subMeshTriangles = mesh.GetTriangles(subMesh);
								for (int i = 0; i < subMeshTriangles.Length; i += 3)
								{
									if (triangleIndex / 3 == i / 3)
									{
										materialIndex = submeshMaterialIndices[subMesh];
										break;
									}
								}
							}

							// Get the correct texture based on material index
							Texture2D textureToSample = materials[0].mainTexture as Texture2D;
							if (materialIndex < materials.Length && materials[materialIndex] != null &&
								materials[materialIndex].mainTexture != null)
							{
								textureToSample = materials[materialIndex].mainTexture as Texture2D;
							}

							if (textureToSample != null)
							{
								// Sample the texture using interpolated UVs
								pixelColor = textureToSample.GetPixelBilinear(interpolatedUV.x, interpolatedUV.y);
							}
						}
					}
				}

				// Set the pixel color in our splatmap texture
				splatmap.SetPixel(x, y, pixelColor);
			}
		}

		// Clean up temporary collider if we added one
		if (objectCollider != null && objectCollider.GetType() == typeof(MeshCollider) &&
			sourceObject.GetComponent<MeshFilter>() == null)
		{
			DestroyImmediate(objectCollider);
		}
	}

	private void ProjectTextureTopDown(GameObject sourceObject, Mesh mesh, Material[] materials, Texture2D sourceTexture, Bounds bounds, TerrainData terrainData, Texture2D splatmap)
	{
		// Simple top-down projection where we just scale the main texture
		int totalPixels = splatmap.width * splatmap.height;

		// Calculate object rotation to properly project texture
		Vector3 sourceRotation = sourceObject.transform.eulerAngles;
		bool needsRotation = sourceRotation != Vector3.zero;

		// For rotated objects, we need to adjust our UV projection
		Matrix4x4 rotationMatrix = Matrix4x4.identity;
		if (needsRotation)
		{
			rotationMatrix = Matrix4x4.Rotate(Quaternion.Euler(-sourceRotation));
		}

		for (int y = 0; y < splatmap.height; y++)
		{
			if (y % 10 == 0)
			{
				ShowProgressBar(y, splatmap.height);
			}

			for (int x = 0; x < splatmap.width; x++)
			{
				// Calculate normalized position (0-1) on the terrain
				float normalizedX = (float)x / splatmap.width;
				float normalizedY = (float)y / splatmap.height;

				// For rotated objects, adjust the UV coordinates
				if (needsRotation)
				{
					// Create a point that's centered (-0.5 to 0.5 range) for rotation
					Vector3 centered = new Vector3(normalizedX - 0.5f, 0, normalizedY - 0.5f);
					// Apply rotation
					Vector3 rotated = rotationMatrix.MultiplyPoint(centered);
					// Convert back to 0-1 range
					normalizedX = rotated.x + 0.5f;
					normalizedY = rotated.z + 0.5f;
				}

				// Clamp values to valid UV range
				float u = Mathf.Clamp01(normalizedX);
				float v = Mathf.Clamp01(normalizedY);

				// Sample source texture
				Color pixelColor = sourceTexture.GetPixelBilinear(u, v);
				splatmap.SetPixel(x, y, pixelColor);
			}
		}
	}

	void ShowProgressBar(float progress, float maxProgress)
	{
		float p = progress / maxProgress;
		EditorUtility.DisplayProgressBar("Creating Terrain...", Mathf.RoundToInt(p * 100f) + " %", p);
	}
}