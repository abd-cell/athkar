import com.android.build.api.dsl.ApplicationExtension
import com.android.build.api.dsl.LibraryExtension
import org.jetbrains.kotlin.gradle.dsl.JvmTarget
import org.jetbrains.kotlin.gradle.tasks.KotlinCompile

allprojects {
    repositories {
        google()
        mavenCentral()
    }
}

/*
 * One JVM target across the whole build: 17, which is what the app module
 * already asks for.
 *
 * This is fussier than it looks, and each line below earned its place:
 *
 *  - A plugin module that declares no Kotlin target falls back to **1.8**, and
 *    then fails outright the moment it depends on a library compiled at 11 or
 *    above ("Cannot inline bytecode built with JVM target 11 into bytecode that
 *    is being built with JVM target 1.8"). `home_widget` did exactly that.
 *  - Fixing only Kotlin makes Kotlin and Java disagree, which AGP refuses just
 *    as firmly ("Inconsistent JVM-target compatibility").
 *  - Java cannot be fixed from the Android DSL alone: the plugin's own build
 *    script sets `compileOptions` to 1.8 and evaluates *after* this file, so the
 *    DSL is set again in `afterEvaluate` to get the last word.
 *  - `options.release` is *not* the lever to reach for: AGP refuses it outright,
 *    because it would bypass the Android bootclasspath.
 *
 * Pinning every subproject rather than patching the one that broke is what
 * keeps this working when the next plugin is added.
 */
subprojects {
    tasks.withType<KotlinCompile>().configureEach {
        compilerOptions.jvmTarget.set(JvmTarget.JVM_17)
    }

    tasks.withType<JavaCompile>().configureEach {
        sourceCompatibility = JavaVersion.VERSION_17.toString()
        targetCompatibility = JavaVersion.VERSION_17.toString()
    }

    afterEvaluate {
        extensions.findByType(LibraryExtension::class.java)?.compileOptions {
            sourceCompatibility = JavaVersion.VERSION_17
            targetCompatibility = JavaVersion.VERSION_17
        }

        extensions.findByType(ApplicationExtension::class.java)?.compileOptions {
            sourceCompatibility = JavaVersion.VERSION_17
            targetCompatibility = JavaVersion.VERSION_17
        }
    }
}

val newBuildDir: Directory =
    rootProject.layout.buildDirectory
        .dir("../../build")
        .get()
rootProject.layout.buildDirectory.value(newBuildDir)

subprojects {
    val newSubprojectBuildDir: Directory = newBuildDir.dir(project.name)
    project.layout.buildDirectory.value(newSubprojectBuildDir)
}
subprojects {
    project.evaluationDependsOn(":app")
}

tasks.register<Delete>("clean") {
    delete(rootProject.layout.buildDirectory)
}
