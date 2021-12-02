WU Fall 2021 | CSMA 302 | Lab #7
---

# Changing Sphere/Color

In the RayTracingMaster.cs on line 155 there are sphere presets for each scene.
In the RayTracingShader.compute on line 236 is the change in color.

# Raytracer

We're making a brute-force basic "Whitted" raytracer.

We will go over ray-sphere intersection in class and implement different BRDF material models and then you will render your own triangle mesh with lighting.

You'll need to implement the following: 
 - Ray-triangle intersection
 - create custom mesh data structures and compute buffers to upload to the GPU

Then you will find your own assets to render:
 - a triangle mesh. You probably want something low poly to keep your render times fast, otherwise it will be hard to move the camera around.
 - custom skybox HDRI (https://polyhaven.com/hdris) - (not the one in the project)
 - place some emissive spheres in your scene with the c# script (not random)

Make 3 renders with different lighting setups, and include them in the root of your project folder (.png) 

## Grading
each bullet is 10 points :

code:
  -  ray triangle intersection implemented
  -  uploading mesh and index buffers to GPU implemented
  -  basic raytracing works (implemented in class) 


assets:
  -  custom lighting spheres from c#
  -  custom HDRI skybox
  -  an interesting model 
  -  add model material parameters (ok if hardcoded in compute shader)


 rendering:
  -  3 "Final Render" images in the root of your project with different lighting setups
  -  the images looking nice and low noise
  -  creativity and interesting scene setup
 
## Due Date

The assignment is due on Wednesday December 1 during 9-1130am studio class (final exam period)

no class is officially scheduled Monday Nov 29 but please reach out to schedule some office hours if you have any questions or problems with your raytracer.

## Resources

[slides](https://docs.google.com/presentation/d/1rSkLqq7CVieGs2DlMLh0jC8Wryd4sFsCT1oa_yOK53k/edit?usp=sharing)

[Raytracing in one weekend](https://raytracing.github.io/books/RayTracingInOneWeekend.html)

[GPU raytracing in Unity](http://three-eyed-games.com/2018/05/03/gpu-ray-tracing-in-unity-part-1/)

[HDRIs](https://polyhaven.com/hdris)

[Low-poly 3D videogame Models](https://www.models-resource.com/)


## Submitting 
(this is also in the syllabus, but consider this an updated version)

1. Disregard what the Syllabus said about Moodle, just submit your work to a branch on github on this repo (branch should be your firstname-lastname)
When you are finished, "Tag" the commit in git as "Complete". You can still work on it after that if you want, I will just grade the latest commit.

2. The project has to run and all the shaders you are using should compile. If it doesn't I'm not going to try to fix it to grade it, I will just let you know that your project is busted and you have to resubmit.  Every time this happens I'll take off 5%. You have 24 hours from when I return it to get it back in, working. 

3. Late projects will lose 10% every 24 hours they are late, after 72 hours the work gets an F. 

4. Obviously plagarism will not be tolerated, there are a small number of students so I can read all your code. Because it is on git it's obvious if you copied some else's. If you copy code without citing the source in a comment, this will be considered plagarism. 
