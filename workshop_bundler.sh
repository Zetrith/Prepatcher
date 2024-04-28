rm -rf Assemblies/

cd Source
dotnet build --configuration Release
cd ..

rm -rf Prepatcher/
mkdir -p Prepatcher
cp -r About Assemblies 1.4 LoadFolders.xml Prepatcher/

# Zip for Github releases
rm -f Prepatcher.zip
zip -r -q Prepatcher.zip Prepatcher

echo "Ok, $PWD/Prepatcher.zip ready for uploading to Workshop"