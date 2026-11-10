# Arquitectura y referencias

Implementación sobre Jellyfin Server tag `v12.4`, commit
`83d0d0e7f76e23e2e085c8ff67d28b772d35967d`:

https://github.com/jellyfin/jellyfin/tree/v12.4

## Negociación

`PlaybackFilter` intercepta la respuesta tipada de
`MediaInfoController.GetPostedPlaybackInfo`. Usa fuentes resueltas por el
servidor autenticado y `MediaInfoHelper.SetDeviceSpecificData` para renegociar
las elegibles. Mantiene los perfiles del cliente y las restricciones del usuario;
no fabrica TranscodingUrl ni deshabilita DirectPlay globalmente.

Wholphin 1.1.1, `PlaybackViewModel.changeStreams`, consume TranscodingUrl cuando
SupportsDirectPlay=false. La reproducción en el dispositivo requiere validación
física adicional, como detalla COMPATIBILIDAD.md.

## Ejecución

`PluginServiceRegistrator` mueve el descriptor singleton original de
ITranscodeManager a un registro keyed privado y expone un decorador.
El original conserva la gestión y liberación de procesos, caché y trabajos.
No se construye un segundo proveedor DI ni se copia TranscodeManager.

`HdrTranscodeManager` comprueba sesión, usuario, dispositivo, fuente, pista,
permisos y vídeo copy antes de añadir los filtros fijos. Las demás operaciones
se delegan al original. `FfmpegArguments` modifica exclusivamente tokens
identificados, conserva los argumentos restantes y rechaza ambigüedades.

La lógica nativa de `EncodingHelper.ShouldRemoveDynamicHdrMetadata` y
`GetBitStreamArgs` puede añadir sus propios filtros: el compositor reconcilia
únicamente las cadenas conocidas y conserva las transformaciones compatibles.

## HLS y caché

`HlsFilter` y `HlsManifest` corrigen CODECS suplementarios y VIDEO-RANGE sobre
manifests afectados. La salida sigue siendo HEVC/PQ. Al conservar DV se utiliza
la strictness nativa de muxing para conservar su configuración fMP4.

`StreamingHelpers.GetOutputFilePath` de esta versión incluye MediaPath,
UserAgent, DeviceId y PlaySessionId en el hash. `SessionPolicies` fija el modo
por sesión para evitar cambios de política sobre segmentos ya generados.
Stop libera la reserva; las políticas abandonadas caducan al limpiar reservas.

## Referencias arquitectónicas examinadas

- https://github.com/jellyfin/jellyfin-plugin-template
- https://github.com/calibrae/jellyfin-anemone
- https://github.com/CrunchyMonkies/jellyops-plugin-jellycode
- https://github.com/upchui/Jellyfin-DirectPlayForce
- https://github.com/SKProCH/JellyfinStreamGenerator

Estas referencias motivaron la investigación de DI, filtros MVC y streaming;
sus implementaciones no se copiaron como reemplazo del gestor original.
El producto no contiene wrappers globales, parches del servidor ni binarios
FFmpeg propios.
