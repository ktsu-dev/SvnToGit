## v2.3.1 (patch)

Changes since v2.3.0:

- Refuse an SVN working copy or other non-repository folder as the local SVN repository [patch] (#163) (matt-edmondson)
- Rename an SVN tag that shares its name with a branch instead of leaving an ambiguous ref [patch] (#164) (matt-edmondson)
- Rename an SVN branch whose name differs from a local branch only by case [patch] (#166) (matt-edmondson)
- Create SVN tags as lightweight tags whatever tag.gpgSign says [patch] (#160) (matt-edmondson)
- Migrate an SVN branch named git-svn instead of silently leaving it out [patch] (#157) (matt-edmondson)
- List git-svn remote refs with for-each-ref so color.ui and column.ui cannot drop them [patch] (#156) (matt-edmondson)
- Escape the proposed Git path in the prompt default instead of cancelling the migration [patch] (#155) (matt-edmondson)
- Regenerate CompatibilitySuppressions.xml for the TFMs the SDK now builds [patch] (#153) (matt-edmondson)
- Propose a Git path beside "." or ".." instead of inside it [patch] (Claude)

