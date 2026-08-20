#!/usr/bin/env bash
#
# MySQL tabanlı yedekleme betiği (Soy Ağacı Yönetim Sistemi).
#
# Kullanım:
#   DB_PASSWORD='...' ./scripts/backup.sh
#
# Ortam değişkenleri (hepsi opsiyonel, varsayılanlar appsettings/User Secrets
# içindeki geliştirme bağlantı dizesiyle aynıdır):
#   DB_HOST          (varsayılan: localhost)
#   DB_PORT          (varsayılan: 3306)
#   DB_USER          (varsayılan: familytree_user)
#   DB_PASSWORD      (zorunlu, varsayılan yok — güvenlik için sabit kodlanmaz)
#   DB_NAME          (varsayılan: FamilyTreeDb)
#   BACKUP_DIR       (varsayılan: ./backups, script konumuna göre)
#   RETENTION_DAYS   (varsayılan: 14 — bu süreden eski yedekler otomatik silinir)
#
# Cron örneği (her gece 03:00, /etc/cron.d/soyagaci-backup):
#   0 3 * * * appuser DB_PASSWORD='...' /opt/soyagaci/scripts/backup.sh >> /var/log/soyagaci-backup.log 2>&1

set -euo pipefail

DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-3306}"
DB_USER="${DB_USER:-familytree_user}"
DB_NAME="${DB_NAME:-FamilyTreeDb}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKUP_DIR="${BACKUP_DIR:-$SCRIPT_DIR/../backups}"

if [ -z "${DB_PASSWORD:-}" ]; then
    echo "HATA: DB_PASSWORD ortam değişkeni ayarlanmamış. Örnek: DB_PASSWORD='...' $0" >&2
    exit 1
fi

if ! command -v mysqldump >/dev/null 2>&1; then
    echo "HATA: mysqldump bulunamadı. MySQL istemci araçlarını kurun." >&2
    exit 1
fi

mkdir -p "$BACKUP_DIR"

TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
OUTPUT_FILE="$BACKUP_DIR/${DB_NAME}-${TIMESTAMP}.sql.gz"

echo "Yedek alınıyor: $DB_NAME ($DB_HOST:$DB_PORT) -> $OUTPUT_FILE"

export MYSQL_PWD="$DB_PASSWORD"

mysqldump \
    --host="$DB_HOST" \
    --port="$DB_PORT" \
    --user="$DB_USER" \
    --single-transaction \
    --routines \
    --triggers \
    --databases "$DB_NAME" \
    | gzip > "$OUTPUT_FILE"

unset MYSQL_PWD

echo "Yedek tamamlandı: $OUTPUT_FILE ($(du -h "$OUTPUT_FILE" | cut -f1))"

if [ "$RETENTION_DAYS" -gt 0 ]; then
    echo "$RETENTION_DAYS günden eski yedekler temizleniyor..."
    find "$BACKUP_DIR" -name "${DB_NAME}-*.sql.gz" -mtime +"$RETENTION_DAYS" -print -delete
fi

echo "Bitti."
