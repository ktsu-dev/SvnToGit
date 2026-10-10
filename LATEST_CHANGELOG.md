## v2.3.0 (minor)

Changes since v2.2.0:

- Fail the migration when git svn clone imports no commits [patch] ([@Claude](https://github.com/Claude))
- Migrate a real local SVN repository given as a plain path in an integration test ([@Claude](https://github.com/Claude))
- refactor: write the free branch-name search as a while loop [patch] ([@Claude](https://github.com/Claude))
- fix: migrate an SVN branch that collides with a local branch under another name [patch] ([@Claude](https://github.com/Claude))
- fix: return a failed result when the Git output path can't be created [patch] ([@Claude](https://github.com/Claude))
- Leave .gitignore as it was; the SDK build rewrote it ([@Claude](https://github.com/Claude))
- Show migration warnings, such as a failed git gc, in the CLI [patch] ([@Claude](https://github.com/Claude))
- Move CI onto the shared ci-shared.yml pipeline ([@Claude](https://github.com/Claude))
- Migrate SVN tags as Git tags and keep branches whose names contain "trunk" [patch] ([@Claude](https://github.com/Claude))
- Run the migration's progress and report through a testable helper ([@Claude](https://github.com/Claude))
- Move migration result and error reporting into testable helpers ([@Claude](https://github.com/Claude))
- Show bracketed paths and git output verbatim instead of crashing [patch] ([@Claude](https://github.com/Claude))
- Keep the migrated repository checked out on trunk [patch] ([@Claude](https://github.com/Claude))

