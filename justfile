set shell := ["bash", "-eu", "-o", "pipefail", "-c"]

test:
    projects=$(find . -path '*/tests/*.fsproj' ! -path '*/tests/Fixtures/*' | sort); \
    if [ -z "$projects" ]; then \
      echo "No test projects found under */tests/"; \
      exit 1; \
    fi; \
    while IFS= read -r project; do \
      echo "==> dotnet run --project $project"; \
      dotnet run --project "$project"; \
    done <<< "$projects"
