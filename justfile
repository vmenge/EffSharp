set shell := ["bash", "-eu", "-o", "pipefail", "-c"]

test:
    projects=$(find . -path '*/tests/*.fsproj' ! -path '*/tests/Fixtures/*' | sort); \
    if [ -z "$projects" ]; then \
      echo "No test projects found under */tests/"; \
      exit 1; \
    fi; \
    while IFS= read -r project; do \
      project_dir=$(dirname "$project"); \
      project_name=$(basename "$project" .fsproj); \
      dll_path="$project_dir/bin/Debug/net10.0/$project_name.dll"; \
      echo "==> dotnet build $project"; \
      dotnet build "$project" --nologo; \
      echo "==> dotnet $dll_path"; \
      dotnet "$dll_path"; \
    done <<< "$projects"
