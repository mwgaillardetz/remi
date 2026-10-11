# Archived Electron version

This directory preserves Rémi 1.x's source and build configuration for reference and rollback. The root project now builds the native Windows application in `native/`.

To run this historical version, use `npm install` then `npm run electron:dev` in this directory. It retains its original dependencies and behavior, including the continuous Three.js renderer and external ffmpeg/Whisper voice pipeline. Its dependencies have not been upgraded as part of the native rewrite.

The repository's pre-existing root `dist/` files are historical build output. They are not used or overwritten by the native build. Any existing edits there are preserved.
