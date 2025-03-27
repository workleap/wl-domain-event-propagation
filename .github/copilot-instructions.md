# Library Maturity Scorecards
A set of rules designed to evaluate the maturity level of a library.

By improving your score, it will help reduce maintenance, reduce cognitive load and improve Developer Experience (DevEx).

## Bronze Level

- librarymaturityscorecard:bronze:Ensure the repository contains a README.md file.
- librarymaturityscorecard:bronze:Set up LinearB on both existing CI and publish pipelines using the shared pipeline from wl-reusable-workflows. Here is an example of that implementation:
linearb:
  needs: [publish]
  uses: workleap/wl-reusable-workflows/.github/workflows/linearb-deployment.yml@main
  with:
    environment: 'release'
  secrets: inherit
- librarymaturityscorecard:bronze:Set up the Jira pipeline using the shared pipeline from wl-reusable-workflows. If there is an existing jira pipeline, you should replace the job by the example. If not, create a new pipeline with the example:
call-workflow-jira:
  uses: workleap/wl-reusable-workflows/.github/workflows/reusable-jira-workflow.yml@main
  with:
    branch_name: ${{ github.head_ref }}
  secrets: inherit
- librarymaturityscorecard:bronze:Set up Semgrep using the shared pipeline from wl-reusable-workflows. If there is an existing semgrep pipeline, you should replace the job by the example. If not, create a new pipeline with the example:
call-workflow-semgrep:
  permissions:
    contents: read
    security-events: write
  uses: workleap/wl-reusable-workflows/.github/workflows/reusable-semgrep-workflow.yml@main

## Silver Level

- librarymaturityscorecard:silver:Ensure the default branch is named main or master.
- librarymaturityscorecard:silver:Set up Renovate to automate dependency management, this would be a renovate.json file. If it is not present, create a new renovate.json file.
- librarymaturityscorecard:silver:Include a Usage section in the README.md file with examples of how to use the library.
- librarymaturityscorecard:silver:Extend the shared configuration in the renovate.json file. Here is an example:
"extends": [
  "github>workleap/renovate-config"
]
- librarymaturityscorecard:silver:Configure renovate.json to auto-merge Workleap dependencies. Here is an an example:
"extends": [
  "github>workleap/renovate-config:all-automerge.json"
]

## Gold Level

- librarymaturityscorecard:gold:Extend renovate.json to include additional auto-merge configurations.
- librarymaturityscorecard:gold:Add Microsoft.CodeAnalysis.PublicApiAnalyzers to detect breaking changes in the public API.
- librarymaturityscorecard:gold:Create PublicAPI.Shipped.txt and PublicAPI.Unshipped.txt files to document the public API surface.