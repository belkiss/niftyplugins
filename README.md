Introduction
============

This repository holds a forked version of Nifty Plugins, orginally written by Jim Tilander (and found at https://github.com/jtilander/niftyplugins)

Note that NiftySolution was removed from this repository.

NiftyPerforce, belkiss' fork
============================

[![Build](https://github.com/belkiss/niftyplugins/actions/workflows/build-and-publish.yml/badge.svg)](https://github.com/belkiss/niftyplugins/actions/workflows/build-and-publish.yml)

The biggest changes from the original NiftyPerforce are:
  * support for recent Visual Studio versions, from vs2017 to vs2022
  * proper registration of NiftyPerforce in the UI, so it can be removed without leaving anything behind
  * CI using GithubActions

Download
--------

* From Visual Studio Marketplace
  - [vs2017/vs2019](https://marketplace.visualstudio.com/items?itemName=belkiss.NiftyPerforceLegacyBelkissFork)
  - [vs2022](https://marketplace.visualstudio.com/items?itemName=belkiss.NiftyPerforceBelkissFork)

* From Github
  - [vs2017/vs2019](https://github.com/belkiss/niftyplugins/releases/latest/download/NiftyPerforceLegacy.vsix)
  - [vs2022](https://github.com/belkiss/niftyplugins/releases/latest/download/NiftyPerforce.vsix)

Configuration
-------------

It is recommended not setting any connection info in nifty but have UseSystemEnv set to true (which is the default).

Ensure you have a P4CONFIG environment variable by typing `p4 set` in a terminal:

```
> p4 set
P4CONFIG=p4config.txt (set) (config 'noconfig')
...
```

If you do not have P4CONFIG, set it to a name (usually p4config.txt, see p4 documentation):

```
> p4 set P4CONFIG=p4config.txt
```

Then create a p4config.txt file at the root of each and every workspace you work in, like this:

```
P4PORT=ssl:ida:3548
P4USER=joe
P4CLIENT=joes_client
```

Since nifty invokes p4 (or p4vc) directly, all the operations will work in the proper server/workspace :)
