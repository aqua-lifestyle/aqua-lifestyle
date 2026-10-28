#!/usr/bin/env bash
set -euo pipefail
# Builds code only. Accepts no database target, credential or production records.
cutover_root=$(git rev-parse --show-toplevel)
cutover_manifest="$cutover_root/tools/operations/yoco-receiver54.json"
cutover_out=${1:?Usage: build-yoco-receiver54.sh ABSOLUTE_NEW_OUTPUT_DIRECTORY}
[[ $cutover_out == /* && ! -e $cutover_out ]] || { printf 'A new absolute output directory is required.\n' >&2; exit 1; }
mapfile -t cutover_pins < <(node --input-type=module - "$cutover_manifest" <<'JS'
import fs from 'node:fs';
const m=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));
if(m.contract!=='Aqua.YocoReceiver54.SourceV1'||m.databaseMigrations!==54||
 ![m.baseline,m.source,m.tree].every(x=>/^[0-9a-f]{40}$/.test(x)))process.exit(1);
for(const x of [m.baseline,m.source,m.tree])process.stdout.write(x+'\n');
JS
)
[[ ${#cutover_pins[@]} == 3 ]] || { printf 'Invalid pinned receiver source contract.\n' >&2; exit 1; }
cutover_base=${cutover_pins[0]}; cutover_sha=${cutover_pins[1]}; cutover_tree=${cutover_pins[2]}
[[ $(git rev-parse "$cutover_sha^{tree}") == "$cutover_tree" ]]
git merge-base --is-ancestor "$cutover_base" "$cutover_sha"
# No migrations, application rewrites, package changes, frontend or schema files
# may be smuggled into the separately admitted compatibility artifact.
while IFS= read -r cutover_file; do
  case "$cutover_file" in
    docker/api/Dockerfile|\
    AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Core/Configuration/YocoCutover.cs|\
    AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.EntityFrameworkCore/EntityFrameworkCore/YocoCutoverStore.cs|\
    AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Application/Payments/Yoco/YocoCutoverService.cs|\
    AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Application/Payments/Yoco/YocoPaymentNotificationProcessor.cs|\
    AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Application/Payments/Yoco/YocoCheckoutGateway.cs|\
    AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Application/ProgrammeParticipations/PilotApplicationBoundaryInterceptor.cs|\
    AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Web.Core/AqualLifeStyleWebCoreModule.cs|\
    AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Web.Host/Controllers/YocoCutoverController.cs|\
    AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Web.Host/Controllers/YocoPaymentsController.cs|\
    AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Web.Host/Startup/YocoCutoverStartup.cs|\
    AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Web.Host/Startup/Startup.cs|\
    AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Web.Host/Startup/ApplicationRecoveryMiddleware.cs|\
    AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Web.Host/Startup/AqualLifeStyleWebHostModule.cs) ;;
    *) printf 'Receiver patch exceeds the compatibility contract.\n' >&2; exit 1 ;;
  esac
done < <(git diff --name-only "$cutover_base" "$cutover_sha")
cutover_temp=$(mktemp -d "${TMPDIR:-/tmp}/aqua-yoco-receiver-code.XXXXXXXX")
trap 'rm -rf -- "$cutover_temp"' EXIT
git -C "$cutover_root" archive "$cutover_sha" | tar -x -C "$cutover_temp"
cutover_project="$cutover_temp/AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Web.Host/AqualLifeStyle.Web.Host.csproj"
dotnet restore "$cutover_project" -p:Configuration=Release
dotnet publish "$cutover_project" --configuration Release --no-restore --output "$cutover_out" \
  -p:DefineConstants=YOCO_CUTOVER_COMPAT -m:1 -p:UseSharedCompilation=false
cp "$cutover_manifest" "$cutover_out/receiver-source.json"
printf 'Receiver source: %s; tree: %s; baseline schema: 54.\n' "$cutover_sha" "$cutover_tree"
