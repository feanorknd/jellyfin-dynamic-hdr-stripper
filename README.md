# Jellyfin Dynamic HDR Stripper

**Dolby Vision y HDR10+ selectivos, una sola película original.**

Plugin para Jellyfin que elimina metadatos dinámicos Dolby Vision y/o HDR10+
durante la reproducción de vídeos HEVC con una capa base HDR10 válida.
Jellyfin genera HLS mediante **remux y copia de vídeo**, sin recodificar la
imagen ni modificar el archivo original. Puedes aplicarlo únicamente a los
dispositivos seleccionados y mantener la reproducción original en el resto.

[Descargar v1.0.0](https://github.com/feanorknd/jellyfin-dynamic-hdr-stripper/releases/tag/v1.0.0)
· [Manifest del repositorio](https://raw.githubusercontent.com/feanorknd/jellyfin-dynamic-hdr-stripper/main/manifest.json)
· [Compatibilidad y validación](docs/COMPATIBILIDAD.md)
· [Arquitectura](docs/ARQUITECTURA.md)

## Compatibilidad

| Componente | Versión objetivo |
|---|---|
| Jellyfin **Server** | **12.4.0.0**, comprobada también al cargar el plugin |
| Runtime | .NET 10 |
| Contenedor | Imagen Docker oficial `jellyfin/jellyfin:12.4` |
| FFmpeg | `8.1.3-Jellyfin`, incluido en la imagen oficial |
| Entrega | HLS con segmentos MPEG-TS o fMP4 |

La versión de Jellyfin Web no determina la compatibilidad. El catálogo declara
`targetAbi: 12.4.0.0`, pero la DLL exige exactamente esa versión de Server.

**Estado de v1.0.0:** integración y procesamiento real validados en laboratorio.
La reproducción y activación HDR en Wholphin 1.1.1 / ExoPlayer / Xiaomi Mi Box S /
Hisense siguen pendientes de validación física. El número de release no supone
certificación para ese dispositivo. Consulta los [resultados y límites](docs/COMPATIBILIDAD.md).

## Instalación desde el panel de Jellyfin

1. Confirma la versión de **Server** en el panel o en el registro de arranque.
2. Abre **Panel de control → Plugins → Repositorios → Añadir**.
3. Introduce:

   **Nombre:** `Dynamic HDR Stripper`

   **URL:**

   ```text
   https://raw.githubusercontent.com/feanorknd/jellyfin-dynamic-hdr-stripper/main/manifest.json
   ```

4. Guarda, abre el **Catálogo** e instala **Dynamic HDR Stripper**.
5. Reinicia el contenedor Jellyfin.
6. Abre la configuración del plugin y selecciona los clientes afectados.

El plugin comienza **desactivado**, con **Dry Run activo**, sin clientes
seleccionados y sin aplicación global.

### Instalación manual

Descarga `DynamicHDRStripper_1.0.0.zip` desde
[Releases](https://github.com/feanorknd/jellyfin-dynamic-hdr-stripper/releases).
Comprueba el archivo con `SHA256SUMS`, publicado junto al ZIP.

Con el contenedor detenido, extrae el ZIP en el volumen persistente de Jellyfin:

```text
/config/plugins/Dynamic HDR Stripper_1.0.0.0/
├── Jellyfin.Plugin.DynamicHdrStripper.dll
├── meta.json
├── README.md
├── LICENSE
└── docs/
```

La DLL y `meta.json` deben quedar directamente en esa carpeta. Inicia el
contenedor después. No necesitas sustituir FFmpeg, compilar Jellyfin ni crear
una imagen Docker personalizada.

## Los cuatro modos

Las casillas **Eliminar Dolby Vision** y **Eliminar HDR10+** son independientes:

| Eliminar DV | Eliminar HDR10+ | Comportamiento |
|:---:|:---:|---|
| No | No | Passthrough: negociación original |
| Sí | No | Elimina DV y conserva HDR10+ cuando existe |
| No | Sí | Elimina HDR10+ y conserva DV cuando existe |
| Sí | Sí | Elimina ambos, conservando la base HDR10 |

Filtros utilizados:

```text
dovi_rpu=strip=1
hevc_metadata=remove_hdr10plus=1
```

Se integran con los filtros compatibles que ya haya generado Jellyfin, incluido
`hevc_mp4toannexb`. Al conservar DV se conserva también su configuración fMP4
usando el mismo ajuste de muxing `-strict -2` que utiliza Jellyfin nativo.

## Primera prueba con Wholphin / Xiaomi

1. Mantén **Aplicar globalmente** desactivado.
2. En **Clientes incluidos**, añade una regla con el DeviceId real del equipo:

   ```text
   Wholphin | DEVICE-ID-DE-TU-XIAOMI |
   ```

3. Activa el plugin y deja **Dry Run** marcado. Reproduce un vídeo elegible y
   comprueba en el registro una decisión `eligible`.
4. Para el primer ensayo, activa la eliminación de **ambos** formatos.
5. Desmarca Dry Run, guarda, **detén la reproducción y vuelve a iniciarla**.
6. Comprueba HDR en la pantalla, audio, subtítulos y seeking. Comprueba también
   que un cliente excluido mantiene la reproducción original.

Eliminar solo DV conserva HDR10+ en las pruebas de flujo. Que esa combinación
resuelva la activación HDR de una Xiaomi concreta debe probarse por separado.

### Reglas de clientes

Formato: una regla por línea, con tres campos separados por `|`:

```text
Nombre de aplicación | DeviceId | Nombre del dispositivo
```

- Los campos no vacíos de una regla se combinan con **AND**.
- Las distintas reglas se combinan con **OR**.
- Aplicación y dispositivo: coincidencia de subcadena sin distinguir mayúsculas.
- DeviceId: coincidencia exacta; no es un token ni el ID del usuario.
- Las exclusiones siempre prevalecen. Una regla vacía no selecciona clientes.
- La aplicación global es opt-in y sigue respetando exclusiones y elegibilidad.

## Qué vídeos se procesan

El plugin exige HEVC Main 10, 10 bits, PQ/SMPTE2084, BT.2020 y matriz BT.2020nc.
El soporte inicial comprende:

- DV Profile 8, compatibility ID 1, RPU y base presentes, sin enhancement layer.
- HDR10+ sin DV con una base HDR10 verificada.

Se omiten HDR10 puro, SDR y perfiles DV incompatibles o incompletos. **No se
convierte Profile 5 a HDR10.** P7, P8.2, HLG y separación de enhancement layer
no están habilitados.

También se requieren una fuente de archivo con una pista de vídeo, un cliente
que anuncie HEVC HLS y permiso de remux. Se respetan los permisos de audio.
El usuario no necesita permiso de recodificar vídeo. Subtítulos que requieran
burn-in no pueden usarse en una sesión transformada con copia de vídeo.

## Configuración y operación

- **Dry Run:** registra decisiones sin alterar la reproducción.
- **Clientes remotos:** desactivados por defecto; se usa la definición de red
  local del servidor. Esta opción no habilita fuentes multimedia remotas/live TV.
- **Concurrencia:** máximo dos sesiones afectadas o negociadas por defecto.
  Al alcanzar el límite se conserva la negociación original para nuevas sesiones.
- **Caducidad:** reservas abandonadas caducan según el intervalo configurado;
  Stop libera la reserva. Una URL de sesión perdida devuelve conflicto y exige
  volver a solicitar PlaybackInfo.
- **Política por sesión:** cambiar ajustes o desactivar el plugin afecta a nuevas
  negociaciones. Detén la reproducción para cambiar inmediatamente de modo.
- **Logs:** los diagnósticos propios redactan rutas y tokens. Los logs nativos
  de Jellyfin/FFmpeg conservan su comportamiento y pueden incluir rutas.
- **Caché:** Jellyfin gestiona procesos, segmentos y limpieza. El remux puede
  avanzar rápidamente; configura el throttling y el disco temporal del servidor.

No hay fallback del plugin a vídeo recodificado o SDR cuando falla una sesión
transformada. Filtros incompatibles o trabajos que no sean vídeo `copy` se
rechazan antes de ejecutar FFmpeg.

## Compilar desde el código fuente

Requisitos: Python 3 y Docker, con contenedores Linux y acceso a las imágenes
oficiales. Desde la raíz del repositorio:

```bash
python3 scripts/build.py
```

El script extrae los ensamblados de la imagen oficial fijada por digest en
`.build/host`, compila con el SDK .NET 10 en un contenedor independiente y genera:

```text
dist/DynamicHDRStripper_1.0.0.zip
dist/SHA256SUMS
dist/manifest.json
```

No inicia un servidor Jellyfin ni despliega el plugin. El ZIP contiene únicamente
la DLL propia, metadatos y documentación; los ensamblados del servidor quedan
como referencias de compilación locales. `dist/manifest.json` describe el ZIP
generado; solo el mantenedor debe publicarlo junto al mismo ZIP y su checksum.

También puedes compilar con SDK .NET 10 local y ensamblados del servidor objetivo:

```bash
dotnet build src/Jellyfin.Plugin.DynamicHdrStripper -c Release \
  -p:HostAssemblies=/ruta/a/ensamblados/oficiales
python3 scripts/build.py --package-only
```

## Actualizar, deshabilitar y desinstalar

- **Actualizar desde catálogo:** instala la versión disponible y reinicia
  Jellyfin. Revisa siempre la ABI objetivo y las notas de la release.
- **Deshabilitar:** desmarca Activar plugin y comienza una nueva reproducción;
  la configuración se conserva.
- **Desinstalar:** usa la opción del panel de plugins y reinicia. Para retirada
  manual, elimina únicamente la carpeta de este plugin con el contenedor parado.
- **Rollback manual:** restaura el ZIP anterior compatible con el servidor,
  evitando dejar simultáneamente dos carpetas con versiones activas del plugin.

No se migran bases de datos ni se alteran los originales. Los cambios se limitan
a sesiones seleccionadas y a la configuración propia del plugin.

## Licencia

GPL-2.0-or-later. Consulta [LICENSE](LICENSE) y el
[texto completo](docs/GPL-2.0.txt). Las referencias arquitectónicas y los puntos
de extensión verificados se describen en [Arquitectura](docs/ARQUITECTURA.md).
