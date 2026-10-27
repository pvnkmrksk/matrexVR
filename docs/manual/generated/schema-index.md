# Full schema field atlas

The bundled schemas currently emit **2,821 property paths across 8 families**. This index includes nested arrays and phase payloads, current animal/agent names and the accepted legacy aliases. It is generated with the static manual from the same JSON used by the configuration studio.

Use the field pages for exact type/default/constraint data. Use the [written parameter reference](../../configuration-reference.md) for units, precedence and experimental meaning. The schema cannot replace cross-field production validation or physical calibration.

| Family | Emitted paths | Field atlas |

| --- | --- | --- |

| `system` | 58 | [System fields](schema-system.md) |

| `sequence` | 44 | [Sequence fields](schema-sequence.md) |

| `choice` | 821 | [Choice fields](schema-choice.md) |

| `swarm` | 933 | [Swarm fields](schema-swarm.md) |

| `kannadi` | 637 | [Kannadi fields](schema-kannadi.md) |

| `optomotor` | 187 | [Optomotor fields](schema-optomotor.md) |

| `rugosity` | 14 | [Rugosity fields](schema-rugosity.md) |

| `dynamic-choice` | 127 | [Dynamic-Choice fields](schema-dynamic-choice.md) |

## Read the contract correctly

A default is the value emitted by the schema generator, not an instruction to inject it into every user JSON file. Opening inherited form controls preserves omission. Scene/prefab defaults and controller application rules remain relevant.

Required means the parent schema emitted that field in its required list. Optional does not mean every combination is executable: production loaders validate modes, references, periods, layer capacity and other cross-field relationships.

UI soft slider bounds, unit labels and visibility/enable rules describe the editor. They do not silently clamp a saved value or delete values hidden by another mode.

## Download the exact JSON

[Current bundled schema JSON](schema-source.json) is copied without changing its bytes. It retains all emitted schema metadata, including UI annotations. [Schema builder source](https://github.com/pvnkmrksk/matrexVR/blob/main/Assets/Editor/ConfigurationSchemaBuilder.cs) explains generation.
