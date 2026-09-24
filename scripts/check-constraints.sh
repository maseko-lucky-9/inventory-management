#!/usr/bin/env bash
# H2/H3: PostgreSQL only, no ORM, no SQL-generating helper. Prints offenders and exits 1; silent when clean.
set -uo pipefail
cd "$(dirname "$0")/.."
deny='EntityFrameworkCore|NHibernate|ServiceStack|OrmLite|Dapper\.Contrib|Dapper\.SimpleCRUD|Dapper\.FastCrud|Dapper\.SqlBuilder|Dommel|DapperExtensions|SqlKata|linq2db|RepoDb|Marten|PetaPoco|Sqlite|SqlClient|InMemory'
hits=$(find . \( -name .git -o -name node_modules -o -name bin -o -name obj \) -prune -o \
  \( -name '*.csproj' -o -name '*.props' -o -name '*.targets' \) -type f -print |
  while read -r file; do grep -EiH "$deny" "$file"; done)
if [ -n "$hits" ]; then
  echo "$hits"
  exit 1
fi
