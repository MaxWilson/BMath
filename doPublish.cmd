pushd \code\BadanarniMath
git checkout master && xcopy public\* publish\ /y && fable --target prod && webpack -p && git checkout gh-pages && xcopy publish\* /y . && git add . && git commit -m "New version" --amend && git push -f && git checkout master
